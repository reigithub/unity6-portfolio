# Unity Dedicated Server デプロイ

Unity 6 + Photon Fusion 2 の Dedicated Server を GCE (Container-Optimized OS) + Managed Instance Group (MIG) にデプロイする。Cloud Run は UDP 非対応のため使用不可。

## ファイル構成

| ファイル | 用途 |
|---------|------|
| `Dockerfile` | Linux Server ビルド出力をコンテナ化する Dockerfile |
| `prod/deploy.ps1` | Windows (PowerShell) からの本番デプロイスクリプト |
| `prod/deploy.sh` | Linux/macOS (bash) からの本番デプロイスクリプト |
| `prod/cloudbuild.yml` | Cloud Build からの自動デプロイ設定 |
| `prod/docker-compose.yml` | ローカル動作確認用 |
| `prod/.env.example` | 本番デプロイ用環境変数テンプレート |

## デプロイの流れ

1. Unity Editor で Linux Dedicated Server をビルド (`src/Game.Client/Builds/Server/Linux/`)
2. `prod/.env` を `.env.example` から作成し、PROJECT_ID 等を設定
3. (初回のみ) `./deploy.ps1 -SetupInfra` または `./deploy.sh --setup-infra` で firewall / health check / Secret IAM を作成
4. `./deploy.ps1` または `./deploy.sh` で build → push → Template 作成 → MIG ローリング更新

## デプロイスクリプトのオプション

```
-BuildOnly / --build-only           build + push のみ（deploy しない）
-SkipBuild / --skip-build           build をスキップ（既存 image で deploy）
-ImageTag / --image-tag TAG         Artifact Registry image tag (default: latest)
-TemplateSuffix / --template-suffix Instance Template の name suffix
                                    省略時は "{ImageTag}-{UnixTime}" を自動付与
                                    （再実行で alreadyExists 衝突回避 + ロールバック互換性）
-InitialDelay / --initial-delay     autohealing initial-delay 秒 (default: 180)
-Force / --force                    rolling-action 進行中でも実行
                                    （接続中ユーザーは強制切断）
-SetupInfra / --setup-infra         GCE インフラ初期セットアップ
-Tag / --tag                        DEPRECATED: --image-tag と --template-suffix の両方に適用
                                    次期メジャー版で削除予定
```

## 既知の制約 / 運用上の注意

### Photon Fusion DS のセッション切断

`deploy.ps1` / `deploy.sh` / `cloudbuild.yml` は `--minimal-action=replace` を使用するため、
**ローリング更新中に接続中の全プレイヤーが切断**される。本番運用ではプレイヤーがいない
時間帯（メンテナンス枠）で実行するか、将来 drain mode（新規セッション受付停止 + 既存
セッション自然終了待ち）を実装することを検討する。

### CI/CD で必要な IAM 権限

`gcloud artifacts docker images describe`（image manifest 事前検証）を呼ぶため、
CI service account には `roles/artifactregistry.reader` が必要。

その他必要な権限:

| ロール | 用途 |
|-------|------|
| `roles/compute.instanceAdmin.v1` | Instance Template + MIG 操作 |
| `roles/secretmanager.secretAccessor` | UNITY_SERVER_AUTH_SESSION_SECRET 取得 |
| `roles/iam.serviceAccountUser` | GCE デフォルト SA への impersonation |
| `roles/artifactregistry.reader` | image manifest 検証 |
| `roles/artifactregistry.writer` | image push（Cloud Build SA で必要） |

### autohealing initial-delay

Unity DS image (~1-2GB) 初回 pull + Mono/IL2CPP 初期化 + Fusion StartServerAsync で
合計 120-180s が現実値。デフォルト `--initial-delay=180` で運用。
image サイズ増加時は `-InitialDelay 240` 等で延長すること。

### Instance Template の自動 GC

`-TemplateSuffix` 省略時は UnixTime suffix 付きで毎回新規 Template が作成される。
**MIG が参照していない古い Template は手動で削除する**:

```bash
# 30 日前以前の template を確認
gcloud compute instance-templates list \
  --filter='name~unity-server-template AND creationTimestamp<-P30D' \
  --sort-by=creationTimestamp

# 削除
gcloud compute instance-templates delete <name> --quiet
```

### rolling-action 二重実行防御

`./deploy.ps1` / `./deploy.sh` は MIG の `status.versionTarget.isReached` を確認し、
進行中の rolling-action がある場合は `-Force` / `--force` なしで中止する。
緊急時のみ `-Force` で上書き可能（接続中ユーザーが切断される）。
