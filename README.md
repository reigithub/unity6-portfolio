# ポートフォリオ

Unity6クライアント + ゲームバックエンド基盤 + オンラインマルチプレイ基盤を含むゲーム開発ポートフォリオ（モノレポ構成）

[English version is here](README.en.md)

## プロジェクト概要

* **Unity × サーバー × インフラをモノレポで一括実装** — Unity6クライアント / REST APIバックエンド(ASP.NET Core) / データベース(PostgreSQL) / CI/CD(GitHub Actions)
* **Photon Fusion 2 オンラインマルチプレイ基盤** — P2Pマルチプレイ/サーバー権威型シングルプレイ・マルチプレイに対応
* **LiveOps配信基盤** — GitHub Actionsセルフホストランナー上にアセットビルドパイプライン構築 + Cloudflare R2 CDNに自動アップロード + Addressablesアセットを開発/本番アプリからロード
* **マスターデータ基盤** — 自作CLIツールでProtobufスキーマ駆動のマスタデータを生成、Client/Server/Realtimeなどを同一スキーマからデプロイターゲット別バイナリ生成(MasterMemory用)
* **アセンブリ分割** — Assembly Difinitionで異なるゲームアプリを1プロジェクトで構築・検証可能にし、循環参照を構造的に防止
* **自動テスト基盤** — PR時などに自動テスト実行、コード品質チェック、脆弱性チェックなどを自動実行するGithubActionsワークフローを構築

> **アーキテクチャ詳細**: [ARCHITECTURE.md](ARCHITECTURE.md)（全11章、ADR 14件）

## プロジェクト背景・目的
* 開発現場で欠かせないエコシステムを構築して効率化しつつ、理解を深めること
* 現場で使用される技術・モダン技術で構築することによる現場環境・最新へのキャッチアップ
* クライアント+バックエンド+インフラを含めたフルスタックで開発することでゲームアプリ全体像を理解する

## デモ動画
| P2Pマルチプレイ                                                                             |
|-------------------------------------------------------------------------------------------|
| [![P2P](http://img.youtube.com/vi/yKuJerdvdhA/0.jpg)](https://youtu.be/yKuJerdvdhA "P2P") |

| シングルプレイ(サーバー権威型)                                                                   |
|---------------------------------------------------------------------------------------------|
| [![Demo](http://img.youtube.com/vi/VYO7xeJUYHk/0.jpg)](https://youtu.be/VYO7xeJUYHk "Demo") |

## スクリーンショット

### スコアアタックゲーム(MVCパターン)
| タイトル | ゲームプレイ | リザルト |
|---------|------------|---------|
| ![タイトル](src/Game.Client/Documentation/Screenshots/mvc_title.png) | ![ゲームプレイ](src/Game.Client/Documentation/Screenshots/mvc_gameplay.png) | ![リザルト](src/Game.Client/Documentation/Screenshots/mvc_result.png) |

### サバイバーゲーム(MVPパターン)
| タイトル | ゲームプレイ | レベルアップ |
|---------|------------|-------------|
| ![タイトル](src/Game.Client/Documentation/Screenshots/mvp_title.png) | ![ゲームプレイ](src/Game.Client/Documentation/Screenshots/mvp_gameplay.png) | ![レベルアップ](src/Game.Client/Documentation/Screenshots/mvp_levelup.png) |

### シェーダー・エフェクト
| トゥーンシェーダー | ディゾルブエフェクト |
|------------------|-------------------|
| ![トゥーン](src/Game.Client/Documentation/Screenshots/shader_toon.png) | ![ディゾルブ](src/Game.Client/Documentation/Screenshots/shader_dissolve.png) |

### エディター拡張
![エディターウィンドウ](src/Game.Client/Documentation/Screenshots/editor_window.png)

| データベース管理 | ゲーム環境設定 |
|-----------------|---------------|
| ![データベースウィンドウ](src/Game.Client/Documentation/Screenshots/database_window.png) | ![ゲーム環境設定](src/Game.Client/Documentation/Screenshots/game_environment_window.png) |

---

## ゲームプレイ動画

### MVC: ScoreTimeAttack
![MVCゲームプレイ](src/Game.Client/Documentation/GIFs/mvc_gameplay.gif)

### MVP: Survivor
![MVPゲームプレイ](src/Game.Client/Documentation/GIFs/mvp_gameplay.gif)

### シーン遷移・エフェクト
| シーン遷移 | エフェクト集 |
|-----------|-------------|
| ![シーン遷移](src/Game.Client/Documentation/GIFs/scene_transition.gif) | ![エフェクト](src/Game.Client/Documentation/GIFs/effects_showcase.gif) |

### エディターツール
![エディターツール](src/Game.Client/Documentation/GIFs/editor_tool.gif)

---

## アーキテクチャ概要

### モノレポ構成
```
┌─────────────────────────────────────────────────────────────┐
│                     Unity6Portfolio                          │
│                      (モノレポ)                               │
└─────────────────────────────────────────────────────────────┘
        ↓              ↓              ↓              ↓
┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
│Game.Client │  │Game.Server │  │Game.Realtime│  │ Game.Shared│
│ (Unity 6)  │  │ (REST API) │  │(gRPC/Hub)  │  │(.NET+Unity)│
└────────────┘  └────────────┘  └────────────┘  └────────────┘
        ↘              ↓              ↓              ↙
               ┌─────────────────────────────┐
               │    共有DTO/IF (Game.Shared)  │
               │  Unary RPC / Hub インターフェース │
               └─────────────────────────────┘
```

### クライアント内アーキテクチャ
```
┌─────────────────────────────────────────────────────────────┐
│                        Game.App                              │
│              (エントリーポイント・ゲームモード選択)             │
└─────────────────────────────────────────────────────────────┘
                    ↓                    ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│      Game.MVC.Core          │  │      Game.MVP.Core          │
│   (MVCパターン基盤)          │  │   (MVPパターン基盤)          │
│   GameServiceManager        │  │   VContainer/DI             │
└─────────────────────────────┘  └─────────────────────────────┘
            ↓                                ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│  Game.MVC.ScoreTimeAttack   │  │    Game.MVP.Survivor        │
│    (タイムアタックゲーム)     │  │   (サバイバーゲーム)          │
└─────────────────────────────┘  └─────────────────────────────┘
            ↖                                ↗
               └──────────────┬──────────────┘
                              ↓
              ┌─────────────────────────────┐
              │         Game.Shared         │
              │  (共通ユーティリティ/DTO)    │
              └──────────────┬──────────────┘
                             ↓
              ┌─────────────────────────────┐
              │     Game.Library.Shared     │
              │      (MasterMemory等)       │
              └─────────────────────────────┘
```

---

## 機能一覧

### クライアント設計
* **アセンブリ分割設計**: MVC/MVPパターンを独立した8アセンブリで管理、循環参照を構造的に防止
* **ゲームモード選択**: 起動時のタイトル画面から異なるアーキテクチャのゲームモードを選択可能
* **DI/イベント駆動**: VContainer、MessagePipe Pub/Sub、R3リアクティブ
* **シーン遷移**: async/await（UniTask）による非同期遷移、スリープ/復帰、引数・戻り値、ダイアログスタック
* **ステートマシン**: ジェネリック型コンテキスト・遷移テーブル・O(1)ステート検索。ネットワーク同期にはFusion FSMを使い分け

### ゲームプレイ
* **戦闘システム**: ICombatTarget/IDamageable/IKnockbackableによる統一的な戦闘インターフェース
* **武器システム**: 自動発射・地面設置型武器、汎用オブジェクトプール（WeaponObjectPool&lt;T&gt;、ProfilerMarker統合）
* **敵AIシステム**: ステートマシン駆動（Idle/Chase/Attack/HitStun/Death）、ウェーブスポーン、NavMesh経路探索
* **アイテムシステム**: ドロップグループ抽選（確率テーブル）、マグネット吸引、オブジェクトプーリング
* **セーブデータ**: MemoryPackバイナリシリアライズ、自動保存（30秒間隔・バックグラウンド移行時）

### パフォーマンス最適化
* **エネミーLODシステム**: 距離ベース3段階LOD（Near 20m/Mid 40m/Far）、フレーム分散再分類によるスパイク防止
* **カスタムシェーダー（URP/HLSL）**: ToonLit（Ramp Diffuse + Rim Light + Outline Pass）、CharacterLit/Unlit（Hit Flash + Dissolve）、LOD Far用軽量Unlitシェーダー。全シェーダーGPUインスタンシング対応
* **レンダリング**: URP PC高品質（SSAO、2048影マップ）/ Mobile軽量（RenderScale 0.8、SSAO無効）の2プロファイル
* **Canvas最適化**: 動的UI用Canvas分離（フェード・ロックオン）、CanvasGroup.alpha制御によるリビルド回避

### ネットワーク・サーバー連携
* **HTTP通信基盤**: 指数バックオフリトライ（RetryPolicy）、サーキットブレーカー（自動復帰）、キャッシュフォールバック（期限切れキャッシュ応答）
* **認証・アカウント管理**: ゲストログイン、メール連携、引き継ぎパスワード、セッション自動復元、バックグラウンド自動トークンリフレッシュ（重複排除）
* **ランキングシステム**: スコア送信・取得、Valkey Sorted Setによる高速ランキング（5分TTL）
* **ロビーシステム**: MagicOnion StreamingHubによるリアルタイムロビー（作成/参加/退出/レディ/ゲーム開始）、Valkey永続化
* **マッチメイキング**: キューベース、Redis Pub/Subリアルタイム通知、セッショントークン発行
* **リアルタイムチャット**: SignalR WebSocket + MagicOnionによるルームベースメッセージング
* **リクエスト署名ポリシー**: 宣言的エンドポイントセキュリティ（3種の署名属性）、起動時fail-fastバリデーション

### リアルタイムオンラインゲームプレイ（Photon Fusion 2）
* **オンラインマルチプレイ基盤**: P2P通信モデル、サーバー権威モデル(Server/Clientモード)
* **敵バッチ同期**: サーバーが敵AI制御、10Hzバッチ同期（NetworkArray<512>）、クライアントDead Reckoning補間
* **Dedicated Server**: Linux ヘッドレスビルド、Game.Serverへの自己登録＋ハートビート、HMAC認証、Dockerデプロイ
* **MultiPlayer Playmode**: エディタで4人分まで同時マルチプレイテスト可能、クローン別ユーザーデータパス分離

### 開発基盤
* **マスターデータ管理**: Protobufスキーマ駆動、CLIツール自作（codegen/build/validate/scaffold/export/diff）、デプロイターゲット別バイナリ生成
* **アセット配信**: Addressablesローカル/リモート4環境切替、Cloudflare R2 CDNデプロイ、index.json差分同期、エディタ自動同期
* **CI/CD**: GitHub Actions 7ワークフロー + Docker Self-hosted Runner + Unity Accelerator、Addressablesデプロイ自動化
* **テスト**: 1,148テスト（EditMode 746 + PlayMode 63 + サーバー 339）、XPlat Code Coverage
* **エディター拡張**: 12個のEditorWindow（MasterData管理、DB管理、環境設定、テクスチャ最適化等）
* **コード品質**: StyleCop + Roslyn Analyzer + .editorconfig階層、自動フォーマットチェック

---

## 機能詳細
<details><summary>ゲームモード選択システム</summary>

1. アプリ起動時にGame.Appのタイトル画面を表示
2. 選択されたゲームモードに応じて対応するランチャーを起動
3. 各ゲームモードは独立したアセンブリで実装され、相互に影響しない
4. ゲーム終了時はランチャーをシャットダウンし、タイトル画面に戻ることが可能
5. ApplicationEventsによる疎結合なイベント通知（下位→上位アセンブリ）
</details>

<details><summary>アセンブリ分割設計</summary>

| アセンブリ | 役割 | 依存関係 |
|-----------|------|---------|
| Game.Library.Shared | 共有ライブラリ（Unity/サーバー共用） | MasterMemory, MessagePack |
| Game.Shared | 共通ユーティリティ、インターフェース、DTO | Game.Library.Shared |
| Game.App | エントリーポイント、ゲームモード選択 | Shared, MVC.Core, MVC.ScoreTimeAttack, MVP.Core |
| Game.MVC.Core | MVCパターン基盤、GameServiceManager | Shared |
| Game.MVC.ScoreTimeAttack | タイムアタックゲーム実装 | MVC.Core, Game.Client.MasterData |
| Game.MVP.Core | MVPパターン基盤、VContainer | Shared |
| Game.MVP.Survivor | サバイバーゲーム実装 | MVP.Core, VContainer |
| Game.Client.Realtime | リアルタイムクライアント（MagicOnion） | Shared |
| **Game.Server** | REST API サーバー（ASP.NET Core 9） | Shared, Server.Shared |
| **Game.Realtime** | リアルタイムサーバー（MagicOnion gRPC） | Shared, Server.Shared |
| **Game.Server.Shared** | サーバー共有基盤（JWT, Valkey, Health） | - |

</details>

<details><summary>Game.Shared（共有ライブラリ）</summary>

マスターデータ定義ファイルを共有ライブラリとして分離し、以下のメリットを実現：

1. **クライアント・サーバー共有**: 同じDTOをUnityとASP.NET Coreで共有可能
2. **依存関係の明確化**: 最下層に配置することで循環参照を防止
3. **ビルド時間短縮**: 変更頻度の低いコードを分離することでインクリメンタルビルドを効率化
4. **バージョン管理**: パッケージ単位でバージョン管理が可能

**含まれるコンテンツ:**
- MasterMemory用マスターデータ定義クラス（AudioMaster, ScoreTimeAttackStageMaster等）
- 共通Enum定義（AudioCategory, AudioPlayTag等）
- 共有インターフェース、DTO

**Survivorマスターデータ（11種類）:**
- `SurvivorStageMaster`: ステージ定義（制限時間、初期武器等）
- `SurvivorStageWaveMaster`: ウェーブ定義（出現タイミング、敵数）
- `SurvivorStageWaveEnemyMaster`: ウェーブ内敵構成
- `SurvivorEnemyMaster`: 敵ステータス（HP、攻撃力、移動速度等）
- `SurvivorPlayerMaster`: プレイヤー基本ステータス
- `SurvivorPlayerLevelMaster`: レベル別ステータス（吸引範囲等）
- `SurvivorWeaponMaster`: 武器定義（タイプ、ダメージ、クールダウン等）
- `SurvivorWeaponLevelMaster`: 武器レベル別ステータス
- `SurvivorItemMaster`: アイテム定義（効果値、レアリティ等）
- `SurvivorItemDropMaster`: ドロップ抽選テーブル

</details>

<details><summary>マスターデータ更新システム</summary>

クライアント・サーバー両対応のスキーマ駆動マスターデータ管理システム:

**アーキテクチャ概要:**
```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Proto Schema    │────▶│ Game.Tools CLI  │────▶│ C# MemoryTable  │
│ (masterdata/)   │     │ codegen/build   │     │ クラス生成       │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                      │                        │
         │              ┌───────┴───────┐                │
         ▼              ▼               ▼                ▼
┌─────────────────┐  ┌────────┐  ┌────────────┐  ┌─────────────┐
│ TSV データ      │  │Client  │  │Server      │  │MemoryDatabase│
│ (raw/*.tsv)     │  │.bytes  │  │.bytes      │  │ (実行時)     │
└─────────────────┘  └────────┘  └────────────┘  └─────────────┘
```

**デプロイターゲット（ビットマスク）:**
| ターゲット | 値 | 用途 |
|-----------|---:|------|
| ALL | 0 | 全ターゲット共通（Id, Name等） |
| CLIENT | 1 | Unityクライアントのみ（アセット名等） |
| SERVER | 2 | APIサーバーのみ（内部バランス値等） |
| REALTIME | 4 | リアルタイムサーバーのみ |

**更新方法（3つの選択肢）:**

1. **バッチファイル（推奨）** - ダブルクリックで実行
```
scripts/masterdata/
├── build-all.bat/.sh      # Client + Server 両方生成
├── build-client.bat/.sh   # Client用のみ生成
├── build-server.bat/.sh   # Server用のみ生成
├── codegen.bat/.sh        # C#クラス生成
├── validate.bat/.sh       # TSV検証
└── export-json.bat/.sh    # JSON出力
```

2. **Unity Editor** - MasterDataWindow（Project > MasterMemory > MasterDataWindow）
   - Game.Tools CLIを内部で呼び出し
   - コード生成、バイナリ生成、TSV検証がGUIから実行可能

3. **CLIコマンド直接実行**
```bash
# C#クラス生成（Proto → MemoryTable）
dotnet run --project src/Game.Tools -- masterdata codegen

# バイナリビルド（TSV → .bytes）
dotnet run --project src/Game.Tools -- masterdata build --out-client ... --out-server ...

# スキーマ検証
dotnet run --project src/Game.Tools -- masterdata validate
```

**クライアント側ロード:**
- Addressables経由で`MasterDataBinary.bytes`をロード
- `MasterDataServiceBase`で`MemoryDatabase`を構築

**サーバー側ロード:**
- 起動時にファイルシステムから`masterdata.bytes`を同期ロード
- DIコンテナ経由で`IMasterDataService`を注入

</details>

<details><summary>データベース管理システム</summary>

PostgreSQLデータベースのマイグレーション・シードデータ管理システム:

**マイグレーション:**
```
scripts/migrate/
├── migrate-up.bat/.sh      # 保留中マイグレーション適用
├── migrate-down.bat/.sh    # ロールバック
├── migrate-status.bat/.sh  # 状態確認
└── migrate-reset.bat/.sh   # リセット（全削除＋再作成）
```

**シードデータ:**
```
scripts/seeddata/
├── seed.bat/.sh     # TSV → DB シード
├── dump.bat/.sh     # DB → TSV ダンプ
└── diff.bat/.sh     # TSV差分比較
```

**Unity Editor:**
- DatabaseWindow（Project > Database > DatabaseWindow）
  - マイグレーション操作（Up/Down/Status/Reset）
  - シードデータ操作（Seed/Dump/Diff）
  - スキーマ選択（master/user/all）

**CLIコマンド:**
```bash
# マイグレーション
dotnet run --project src/Game.Tools -- migrate up
dotnet run --project src/Game.Tools -- migrate down --steps 1
dotnet run --project src/Game.Tools -- migrate status
dotnet run --project src/Game.Tools -- migrate reset --force --seed

# シードデータ
dotnet run --project src/Game.Tools -- seeddata seed --tsv-dir masterdata/raw/
dotnet run --project src/Game.Tools -- seeddata dump --out-dir masterdata/dump/
dotnet run --project src/Game.Tools -- seeddata diff --source-dir masterdata/raw/ --target-dir masterdata/dump/
```

</details>

<details><summary>シーン/ダイアログ遷移機能</summary>

1. 非同期処理(async/await)で実装
2. 前のシーンが破棄されていても遷移履歴から再遷移が可能
3. 現在シーンをスリープさせて次のシーンへ遷移でき、戻るとスリープ状態から復帰可能
4. シーン実装は起動前/ロード時/初期化時/スリープ時/復帰時/終了時など様々なタイミングで追加処理を挟む事ができます
5. シーンに任意で引数や戻り値を追加で設定できます
6. 引数つきのシーンであっても、履歴から状態を復元して再度引数を渡して遷移する事が可能
7. ダイアログ(オーバーレイ)は複数開く事が可能で、不正な挙動を防止するためにシーン遷移時に全て破棄されます
</details>

<details><summary>ステートマシーン</summary>

1. ジェネリック型コンテキストを持ち、任意の型を指定できます。
2. 各ステートからコンテキストを参照して、状態管理を行う事ができます
3. 初期時に遷移テーブルを構築でき、各ステートがどのステートから遷移するかルールを設定できます。遷移ルールが1ヶ所に集約/可視化され保守性が向上します。
4. 任意ステートから遷移先に指定できる特別なステートを設定可能で、適切な設定が遷移テーブルに無い場合に遷移が検証/実行されます。
5. ジェネリック型のイベントキー型を指定でき、遷移イベント名をenum等で集約管理できます。遷移先ステート名と一致させると可読性/保守性が向上します。
6. 通常のUpdateに加え、MonoBehaivior.FixedUpdate/LateUpdateにも対応。これにより物理演算やカメラ等の状態と相互に連携できます。
</details>

<details><summary>サバイバーゲームシステム（MVP）</summary>

**戦闘システム**
- `ICombatTarget`: ダメージ・ノックバック・ターゲット機能を統合した戦闘インターフェース
- `IDamageable`, `IKnockbackable`, `ITargetable`: 個別機能のインターフェース
- 敵・プレイヤー共通の戦闘ロジックを実現

**武器システム**
- `SurvivorWeaponBase`: 武器の基底クラス（ダメージ計算、クリティカル、発動率）
- `SurvivorAutoFireWeapon`: 自動発射型武器（最寄りの敵に向けて弾を発射）
- `SurvivorGroundWeapon`: 地面設置型武器（ターゲット位置に円形パターンでダメージエリア生成）
- `WeaponObjectPool<T>`: 汎用オブジェクトプール（弾・エリア共通）
- マスターデータ駆動（レベルごとのステータス・アセット変更対応）

**敵AIシステム**
- `SurvivorEnemyController`: ステートマシン駆動の敵AI
- 状態遷移: Idle → Chase → Attack → HitStun → Death
- `SurvivorEnemySpawner`: ウェーブ管理・スポーン制御
- NavMeshAgentによる経路探索

**アイテムシステム**
- `SurvivorItemSpawner`: 敵撃破時のドロップ管理
- ドロップグループ抽選（確率テーブルによるアイテム決定）
- マグネット吸引機能（範囲内アイテムの自動回収）

**プレイヤーシステム**
- `SurvivorPlayerController`: 移動・HP・スタミナ・無敵管理
- ステートマシン: Normal → Invincible → Dead
- アイテム吸引範囲のレベル連動

**セーブデータシステム**
- `SurvivorSaveService`: ステージ進行状況・クリア記録の保存
- MemoryPackによる高速バイナリシリアライズ
- 自動保存（30秒間隔・バックグラウンド移行時）
- 勝敗確定時の即時保存（データ整合性保証）

**ランキングシステム**
- スコア送信: ステージクリア時に自動送信
- ランキング取得: ステージ別トップ100表示
- 自分の順位: リアルタイム順位確認
- Valkeyキャッシュ: Sorted Setによる高速ランキング取得（5分TTL）
- 本番環境: Cloud Run + Cloud SQL + Memorystore for Valkey

</details>

<details><summary>マルチプレイヤーシステム</summary>

MagicOnion（gRPC StreamingHub）+ Valkey によるリアルタイムマルチプレイヤー基盤:

**通信プロトコル:**
```
┌─────────────┐    REST (HTTP/1.1)     ┌─────────────┐
│ Game.Client │◄──────────────────────►│ Game.Server │
│   (Unity)   │    gRPC (HTTP/2)       │ (REST API)  │
│             │◄──────────────────────►├─────────────┤
│             │   StreamingHub         │Game.Realtime│
│             │◄══════════════════════►│(gRPC/Hub)   │
└─────────────┘                        └──────┬──────┘
                                              │
                                       ┌──────┴──────┐
                                       │   Valkey    │
                                       │  (Redis)    │
                                       └─────────────┘
```

**ロビーシステム:**
- Unary RPC: ロビー作成・参加・退出・検索・情報取得
- StreamingHub: リアルタイムイベント（プレイヤー参加/退出、チャット、レディ状態、ゲーム開始）
- Valkey永続化: ロビー情報、プレイヤー一覧、レディ状態をHash/Stringで管理
- 自動復帰: ゲーム終了後のロビー自動再参加

**マッチメイキングシステム:**
- Redis Pub/Subによるキュー状態リアルタイム通知
- バックグラウンドマッチングプロセッサ
- セッショントークン発行（マッチ参加認証）

**MPPM（Multiplayer Playmode）対応:**
- エディタ内で複数クローンインスタンスを起動しマルチプレイテスト
- クローンごとのデータパス分離（セーブデータ・セッション・オーディオ設定）
- GameBootstrap起動時に自動検出・パス切り替え

</details>

<details><summary>サーバー権威モデル（Photon Fusion 2）</summary>

Survivor マルチプレイの Server/Client モードによるサーバー権威ゲームプレイ:

**プレイヤー状態管理:**
- `[Networked]` プロパティ（HP/Stamina/Speed/IsInvincible）によるサーバー権威状態
- Fusion FSM アドオン（StateBehaviour + StateMachineController）によるステート同期（Normal/Invincible/Dead）
- KCC（Kinematic Character Controller）による移動・回転の予測/補間

**ダメージ処理フロー:**
- エネミー → `TakeDamage()` → `RequestDamage()` → Fusion FSM NormalState が消費 → HP 減算
- サーバーから全クライアントへ `NotifyPlayerDamaged` RPC → MessagePipe → UI 更新

**敵同期方式:**
- サーバーが敵を生成・AI 制御（NavMeshAgent）、10Hz でバッチ同期（NetworkArray<512>）
- クライアントは Dead Reckoning + 補正減衰で位置補間表示
- Spawn/Death は定期同期に統合（`_spawnedNetworkIds` / `_pendingDeaths`）
- 到達不能エネミーはキルカウント非加算で静的回収（Silent Removal）

**View/Presenter 分離:**
- View: プロキシ管理、Dead Reckoning、同期受信（SurvivorEnemyView / ItemView）
- Presenter: Animator / VFX 制御（SurvivorEnemyPresenter / PlayerPresenter）
- Controller: ゲームロジック（サーバー側のみ実行）

</details>

<details><summary>Dedicated Server オーケストレーション</summary>

Unity Dedicated Server の自動登録・セッション管理・ヘルスチェック基盤:

**サーバー起動フロー:**
```
UnityServerBootstrap (IAsyncStartable)
  ├── UnityServerConfigFactory でコンフィグ構築
  │     ├── CLI引数 (--port, --health-port)
  │     ├── 環境変数 (UNITY_SERVER_PORT, GAME_SERVER_URL等)
  │     └── GCEメタデータ (外部IP自動検出, 2秒タイムアウト)
  ├── UnityServerHttpListener 起動 (TCP)
  │     ├── GET /health (Docker HEALTHCHECK)
  │     └── POST /session/start (セッション開始要求)
  ├── Game.Server へ自己登録 (HMAC署名付き)
  └── UnityServerHeartbeatLoop 開始 (30秒間隔)
```

**認証フロー:**
- DS → Game.Server: HMAC-SHA256署名（共有シークレットキー）
- クライアント → DS: Fusion ConnectionToken（MessagePack + HMAC-SHA256バイナリ）
- セッショントークンはValkeyに5分TTLで保存

**Docker構成:**
- ポート: 7777/udp（Fusion）+ 7778/tcp（ヘルスチェック）
- 非rootユーザー（gameserver）で実行
- `docker/unity-server/prod/` に本番デプロイ構成

**エディターツール:**
- `Project > Server > Start/Stop Dedicated Server` メニュー
- .env からポート読み取り、PIDセッション管理
- エディター終了時の自動クリーンアップ

**主要クラス:**

| クラス | 役割 |
|-------|------|
| `UnityServerBootstrap` | DS起動オーケストレーション（IAsyncStartable） |
| `UnityServerConfigFactory` | CLI/環境変数/GCEからコンフィグ構築 |
| `UnityServerAuthProvider` | Fusion ConnectionToken HMAC検証 |
| `UnityServerRegistryApiClient` | Game.Server 登録/ハートビート/解除 API |
| `UnityServerHeartbeatLoop` | バックグラウンドスレッド30秒ハートビート |
| `UnityServerHttpListener` | TCP ヘルスチェック＋セッション管理 |
| `DedicatedServerEditorMenu` | エディターからの DS ビルド＆起動 |

</details>

<details><summary>リクエスト署名ポリシーシステム</summary>

REST API の全エンドポイントに対する宣言的セキュリティポリシー:

**3種の署名属性:**

| 属性 | 用途 | 例 |
|------|------|-----|
| `[SkipRequestSigning]` | 署名不要（匿名認証、リフレッシュ等） | `/api/auth/login`, `/api/auth/refresh` |
| `[RequireUserSignature]` | ユーザーHMAC署名（JWT userId派生キー） | `/api/survivor/scores`, `/api/chat/rooms/*` |
| `[UnityServerSignature]` | DS共有シークレットHMAC署名 | `/api/unity-server/register`, `/heartbeat` |

**fail-fast バリデーション:**
- `RequestSigningPolicyValidator` が起動時に全エンドポイントをスキャン
- 未宣言・複数宣言のエンドポイントを検出し `InvalidOperationException` で即停止
- 新規エンドポイント追加時の署名ポリシー付け忘れを構造的に防止

</details>

<details><summary>エネミーLODシステム</summary>

距離ベースの3段階LODによる描画品質最適化:

**LODティア:**

| ティア | 距離 | 更新頻度 | 内容 |
|--------|------|---------|------|
| Near | < 20m | 毎フレーム | フル品質（アニメーション・影・エフェクト） |
| Mid | 20〜40m | 2フレーム毎 | 中品質（影簡略化） |
| Far | > 40m | 5フレーム毎 | 低品質（アニメーション簡略化・影なし） |

**フレーム分散再分類:**
- LODティア再計算をフレームオフセットで分散配置
- `(FrameCount % interval) == FrameOffset` でスパイクを防止
- 512体同時管理でもフレームレートへの影響を最小化

**CharacterUnlit シェーダー:**
- LOD Far 用の軽量アンリットシェーダー
- ヒットフラッシュ、ディゾルブエフェクト対応
- GPUインスタンシング対応

</details>

<details><summary>HTTP通信基盤</summary>

UnityApiClientによる堅牢なHTTP通信設計:

**リトライポリシー（RetryPolicy）:**
- 指数バックオフ（初期1秒 × 2.0倍、上限30秒）
- リトライ対象ステータスコード別判定（408/429/500/502/503/504）
- プリセット: Default（3回） / Aggressive（5回、初期500ms） / None（リトライなし）

**サーキットブレーカー（CircuitBreakerPolicy）:**
- Closed → Open → HalfOpen の3状態自動遷移
- 連続エラー閾値（デフォルト5回）超過で回路遮断
- Open状態30秒経過でHalfOpenへ自動復帰、成功時にClosedへ復帰
- プリセット: Default / Sensitive（3回/60秒） / Tolerant（10回/15秒）

**キャッシュフォールバック:**
- サーキットOpen時に期限切れキャッシュで応答（`FallbackToCache`）
- TTL指定によるレスポンスキャッシュ

**RequestOptions:**
- タイムアウト制御（デフォルト15秒）、キャッシュ設定、追加ヘッダー
- ビルダーパターン: `RequestOptions.WithCache(TimeSpan)` / `.WithTimeout(seconds)`

</details>

<details><summary>カスタムシェーダー（URP / HLSL）</summary>

URPパイプラインに対応したカスタムHLSLシェーダー群:

| シェーダー | LOD | 主要機能 | Pass数 |
|-----------|-----|---------|-------|
| ToonLit + ToonLighting.hlsl | 300 | Rampテクスチャによるセルシェーディング、Fresnelリムライト、法線オフセットアウトライン | 5 |
| CharacterLit | 300 | ヒットフラッシュ、方向性ノイズディゾルブ、PBRライティング | 4 |
| CharacterUnlit | 100 | LOD Far用軽量版、ヒットフラッシュ+ディゾルブ対応 | 2 |
| Dissolve | 100 | 死亡エフェクト用ディゾルブ | 3 |
| HitFlash | 100 | 被弾エフェクト用フラッシュ | 3 |

**共通仕様:**
- 全シェーダー `#pragma multi_compile_instancing` 対応（GPUインスタンシング）
- メインライト + 追加ライト対応（ToonLit）
- LOD Far（CharacterUnlit）でシャドウ・ライティング計算を省略し描画負荷を軽減

</details>

<details><summary>認証・アカウント管理システム</summary>

サーバー連携による認証・セッション管理システム:

**認証機能 (AuthApiService):**
- ゲストログイン（デバイスフィンガープリント）
- メール/パスワードログイン
- ユーザーID/パスワードログイン
- パスワード忘れ・リセット
- トークンリフレッシュ（自動更新）

**アカウント連携機能:**
- メールアドレス連携/解除
- 引き継ぎパスワード発行（12桁）
- 別端末へのデータ移行

**セッション管理 (AuthSessionService):**
- トークンの暗号化保存（ローカル）
- セッション自動復元（アプリ起動時）
- デバイスフィンガープリント生成

**自動トークンリフレッシュ (AuthSessionRefresher):**
- バックグラウンド5分間隔の定期チェック（JWT 60分有効期限、50分閾値で先行リフレッシュ）
- リアクティブトリガー: ネットワーク復帰時、アプリフォーカス復帰時、シーン遷移時の明示呼び出し
- 並行呼び出しの重複排除（UniTaskCompletionSource共有）
- MessagePipe経由でリフレッシュ結果をシグナル通知

**UI実装:**
- `SurvivorAccountLinkDialog`: メール連携・引き継ぎパスワード発行UI
- プロフィール表示（ユーザーID、レベル、認証タイプ）

</details>

<details><summary>アセット配信システム</summary>

GameEnvironment設定に応じてAddressablesのアセット配信元を切り替え:

**対応環境:**

| GameEnvironment | アセット配信元 | 用途 |
|-----------------|--------------|------|
| Local | Local (StreamingAssets) | 開発・デバッグ |
| Develop | Remote (開発サーバー) | 開発環境テスト |
| Staging | Remote (ステージング) | リリース前検証 |
| Release | Remote (CDN) | 本番配信 |

**切り替え方法:**
- **CI/CD**: 環境変数 `GAME_ENVIRONMENT` から自動設定
- **エディター**: メニュー `Build > Addressables > Switch Profile` から手動切り替え

**主な機能:**
- Addressablesプロファイル切り替えでLocal/Remote自動選択
- 環境変数によるAPI接続先切り替え
- Unity Accelerator によるライブラリキャッシュ共有
- GitHub Actions でのアセットキャッシュ最適化

**エディタ同期機能（UseExistingBuildモード対応）:**
- CIビルド後、他の開発者がUseExistingBuildでプレイ可能
- Play開始前にカタログ存在チェック、不在時は自動ダウンロード促進
- `index.json`（catalogHash + ファイル一覧）でバージョン管理・差分同期
- エディタ拡張: `Window > Game Environment Settings` からカタログバージョン確認/アセットダウンロード可能、アセットカタログ/ダウンロード済みを削除可能

</details>

<details><summary>CI/CD システム</summary>

GitHub Actions + Docker による自動化パイプライン:

**テスト自動化:**

| カテゴリ | テスト数 | 内容 |
|---------|---------|------|
| クライアント EditMode | 746 | ユニットテスト（Service, Model, Extension） |
| クライアント PlayMode | 63 | 統合テスト（Scene, Input, UI） |
| サーバーテスト (Game.Server) | 222 | Controller, Service, Validation, Integration テスト |
| リアルタイムサーバーテスト (Game.Realtime) | 117 | Hub, Service, Filter, Validation テスト |
| **合計** | **1148** | 全パス |

**ワークフロー:**

| ワークフロー | トリガー | 内容 |
|-------------|---------|------|
| unity-test.yml | PR | Unityテスト（Docker/Linux） |
| unity-build.yml | 手動 | マルチプラットフォームビルド（WebGL GitHub Pagesデプロイ対応） |
| unity-server-build.yml | push/PR | Dedicated Serverビルド（Linux） |
| server-test.yml | push/PR, 手動 | サーバーテスト + カバレッジ |
| code-quality.yml | push/PR | フォーマット・静的解析 |
| pr-review.yml | PR | 自動レビューコメント |
| addressables-deploy.yml | 手動 | Addressablesビルド＆Cloudflare R2デプロイ |

**キャッシュ最適化:**
- Unity Accelerator によるライブラリキャッシュ
- GitHub Actions でのアセットキャッシュ共有
- Docker イメージのレイヤーキャッシュ

</details>

<details><summary>その他</summary>

* シーン遷移やオーディオ再生などの共通機能は主にゲームサービスとして分離されています
* マスターデータエディタ拡張はTSVから簡単にバイナリを作成でき、TSV更新後すぐにデータをテストできます。これによって検証サイクルを早めています。テストしたバイナリをそのままビルドやアセット配信で使用できます。
* インゲームシーンはPrefabシーン＋Unityシーンで構成されており、ステージとなるUnityシーンはロジックから分離されています。その為、コード修正なしで新しいステージを追加できます
* アウトゲームシーンは遷移挙動のカスタマイズ性を担保するため、全てPrefabシーンを採用しています
</details>

---

## プロジェクト構成

> 詳細なアセンブリ依存関係・設計ルールは [ARCHITECTURE.md](ARCHITECTURE.md) §3-4 を参照

<details><summary>フォルダ構成</summary>

```
Unity6Portfolio/
├── src/
│   ├── Game.Client/                    # Unity クライアント
│   │   ├── Assets/
│   │   │   ├── MasterData/             マスターデータ(TSV, バイナリ)
│   │   │   └── Programs/
│   │   │       ├── Editor/             エディタ拡張
│   │   │       │   └── Tests/          単体テスト
│   │   │       └── Runtime/
│   │   │           ├── Shared/         共通ユーティリティ
│   │   │           │   ├── Network/    Fusion, MagicOnion通信
│   │   │           │   ├── Services/   認証, トークンリフレッシュ
│   │   │           │   ├── Unity/Server/ DS認証・コンフィグ・レジストリ
│   │   │           │   └── Environment/ 環境変数・CLIヘルパー
│   │   │           ├── App/            エントリーポイント
│   │   │           ├── MVC/            MVCパターン実装
│   │   │           │   ├── Core/       基盤(Services, Scenes)
│   │   │           │   └── ScoreTimeAttack/
│   │   │           └── MVP/            MVPパターン実装
│   │   │               ├── Core/       基盤(VContainer)
│   │   │               └── Survivor/   サバイバーゲーム
│   │   ├── Packages/
│   │   ├── ProjectSettings/
│   │   └── Documentation/              スクリーンショット、GIF
│   │
│   ├── Game.Client.Linked/             # MasterDataブリッジ(.NET SDK形式)
│   │
│   ├── Game.Server/                    # REST API サーバー (ASP.NET Core 9)
│   │   ├── Controllers/                API エンドポイント
│   │   ├── Services/                   ビジネスロジック
│   │   ├── Repositories/              データアクセス (Dapper)
│   │   ├── Hubs/                       SignalR Hub (チャット)
│   │   ├── Middleware/                 リクエスト署名検証, ポリシーバリデーション
│   │   ├── Attributes/                署名ポリシー属性 (3種)
│   │   └── Program.cs
│   │
│   ├── Game.Realtime/                  # リアルタイムサーバー (MagicOnion gRPC)
│   │   ├── Hubs/                       StreamingHub (ロビー, マッチメイキング)
│   │   ├── Services/                   ロビーデータ, マッチング処理
│   │   ├── Filters/                    JWT認証, バリデーション
│   │   └── Program.cs
│   │
│   ├── Game.Server.Shared/             # サーバー共有ライブラリ
│   │   ├── Extensions/                 JWT検証, ユーザーID取得
│   │   ├── Health/                     ヘルスチェック基盤
│   │   └── Valkey/                     Redis/Valkey操作
│   │
│   ├── Game.Shared/                    # 共有ライブラリ (.NET + Unity Package)
│   │   ├── Game.Shared.csproj          .NET プロジェクト
│   │   ├── package.json                Unity パッケージ定義
│   │   └── Runtime/
│   │       └── Shared/
│   │           ├── Dto/                共有DTO
│   │           ├── Enums/              AudioCategory等
│   │           ├── MasterData/         マスターデータ定義
│   │           └── Realtime/           Hub/Service インターフェース
│   │               ├── Hubs/           ILobbyHub, IMatchmakingHub
│   │               └── Services/       ILobbyService, IMatchmakingService
│   │
│   └── Game.Tools/                     # CLIツール (.NET 9)
│
├── masterdata/                         # Protobufスキーマ + TSVデータ
│
├── docker/                             # Docker構成
│   ├── game-server/                    # Game.Server + PostgreSQL + Valkey
│   ├── game-realtime/                  # Game.Realtime (gRPC)
│   ├── observability/                  # OpenTelemetry / Aspire Dashboard
│   ├── migrate/                        # DBマイグレーション
│   ├── unity-accelerator/              # Unity Accelerator キャッシュサーバー
│   ├── unity-ci/                       # Unity CI Runner (GitHub Actions用)
│   └── unity-server/                   # Dedicated Server (Linux ヘッドレス)
│
├── docs/                               # 技術ドキュメント
│
├── scripts/                            # ビルド・フォーマットスクリプト
│
└── test/
    ├── Game.Server.Tests/              # サーバーテスト
    └── Game.Realtime.Tests/            # リアルタイムサーバーテスト
```

</details>

---

## パフォーマンス改善・検証サンプル

大量エネミーの状態管理 + 弾・VFX・ダメージイベント高頻度発生というサバイバーゲームの性質上、GC.Alloc を hot path から排除することを全レイヤーで徹底。

| 対象 | 施策 | 改善結果 |
|------|------|---------|
| シーン遷移 | Task → UniTask 移行 | CPU 実行時間 40% 削減、ゼロアロケーション化（EditMode ベンチマーク実測） |
| ステートマシン | HashSet → Dictionary、LINQ 排除、`[MethodImpl(AggressiveInlining)]` 適用 | 遷移速度 2.05x、メモリ 2.14x 改善（EditMode ベンチマーク実測） |
| Dead Reckoning 補間 | `struct EnemyProxyInterpolation` + Vector3 値型演算のみ | per-entity 0.065-0.069μs、N=500 規模で ~35μs/frame、alloc 0B（EditMode ベンチマーク実測） |
| ネットワーク同期 | `SurvivorNetworkEnemyStateSnapshot[512]` 事前確保バッファで 10Hz sync の `new[]` を排除 | サーバー敵状態同期の GC Alloc **99.9% 削減**（EditMode ベンチマーク実測） |
| 敵描画の LOD | 距離ベース 3 段階 LOD（Near / Mid / Far）+ フレーム分散再分類 | N=500 規模で `EnemyView.Update` Self Time **60% 削減**（PlayMode 統合テスト実測） |
| 弾・VFX・敵・アイテム生成 | `WeaponObjectPool<T>` ジェネリック Pool + 型別 `Dictionary<int, Queue<T>>` Pool | `Instantiate`/`Destroy` spike 排除、弾 100 発級でも GC 安定 |
| 物理クエリ | `OverlapSphere` / `SphereCast` NonAlloc API + `readonly Collider[]` / `RaycastHit[]` バッファ再利用（10 箇所） | 武器ターゲティング・弾衝突・ロックオン等が毎フレ alloc 0 |
| Shader / Animator パラメータ | `Shader.PropertyToID` 27 個 + `Animator.StringToHash` 10 個を `static readonly int` キャッシュ | `SetFloat` / `SetTrigger` 呼出ごとの string lookup alloc 排除 |
| 距離判定 | `sqrMagnitude < threshold * threshold` で sqrt 省略（21 箇所） | 武器最近傍探索・LOD 分類・補間補正判定を高速化 |
| イベント配信 | MessagePipe (`IPublisher` / `ISubscriber`) + R3 `Observable<T>` / `Subject<T>` + 16 個の `readonly struct` シグナル | publish 時 heap alloc ゼロ、Pub/Sub 30+ 箇所で統一 |
| 非同期処理 | UniTask 全面採用（`async void` ゼロ）、コルーチン不使用（`new WaitForSeconds` ゼロ） | Task / Coroutine 由来の state machine alloc 排除 |
| GetComponent キャッシュ | Initialize 時に `TryGetComponent(out _field)` + `GetComponentsInChildren` を field cache 化 | Update 内階層探索ゼロ |

**計測インフラ:** カスタム `ProfilerMarker` 19 箇所（Enemy / Weapon / Pool / VFX / Player 等）を埋め込み、Unity Profiler Timeline 上でボトルネックを可視化。さらに EditMode micro-benchmark（805 テスト）と PlayMode 統合テスト（88 テスト）の 2 段で定量検証。

<details><summary>シーン遷移機能</summary>

* GameSceneService
  - 各種シーン遷移機能をTaskからUniTaskへ変更し、パフォーマンス改善を検証
  - イテレーション数: 10,000
  - CPU実行時間が約40%削減、ゼロアロケーション化、メモリ使用量100%削減

![パフォーマンステスト](src/Game.Client/Documentation/Screenshots/GameSceneServicePerformanceTests.png)

</details>

<details><summary>ステートマシーン</summary>

* 改善項目
  - ステート管理をHashSet→Dictionaryに変更、ステート検索がO(n)からO(1)に改善
  - 遷移時のDictionary検索回数を削減、LINQ使用箇所を改善しアロケーション削減
  - メソッドのインライン化でオーバーヘッドを削減

* 状態遷移のスループット向上
  - イテレーション数: 30,000
  - 遷移時間が平均15%短縮、スループットが平均15%向上

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | 総実行時間 (ms) | 44.848 | 35.295 | 1.27x |
  | 平均遷移時間 (μs) | 0.300 | 0.146 | 2.05x |
  | スループット (ops/s) | 668,934 | 849,991 | 1.27x |

* 状態遷移のメモリアロケーション改善
  - イテレーション数: 10,000

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | メモリ (bytes) | 2,760,704 | 1,290,240 | 2.14x |

</details>

<details><summary>Dead Reckoning 補間（struct + Vector3 値型）</summary>

* `EnemyProxyInterpolation` 構造体による補間状態管理（`src/Game.Client/Assets/Programs/Runtime/MVP/Survivor/Enemy/EnemyProxyInterpolation.cs`）
  - 4 フィールド（`LastSyncPosition` / `Velocity` / `TimeSinceSync` / `CorrectionOffset`）を値型で保持
  - `OnSyncReceived` / `GetPosition` は Vector3 と float の演算のみで alloc 0B
  - ボックス化を防ぐため class ではなく struct で設計

* 実測値（`EnemyProxyInterpolationPerformanceTests`）:

  | n | GetPosition (ms/1000iter) | OnSyncReceived (ms/1000iter) | per-entity | GC Alloc |
  |---|-------------------------:|----------------------------:|:----------:|:--------:|
  | 100 | 6.61 | 6.79 | 0.066-0.068 μs | 0 |
  | 256 | 16.93 | 17.46 | 0.066-0.068 μs | 0-4 KB* |
  | 500 | 33.07 | 34.73 | 0.066-0.069 μs | 0 |
  | 512 | 33.40 | 34.74 | 0.065-0.068 μs | 0-16 KB* |

  *一部サイズで Vector3.Lerp 内部の一時オブジェクト検出あり、本番相当の Release ビルドでは除去される想定

* N=500 規模で補間総コスト ~35μs / frame を実現

</details>

<details><summary>ネットワーク同期 alloc 削減</summary>

* 課題: `SurvivorEnemySpawner.SyncEnemyStatesToNetwork` が 10Hz で `new SurvivorNetworkEnemyStateSnapshot[count]` を毎回 heap alloc
* 改善: `_syncSnapshotBuffer` を `InitializeAsync` 時に 512 枠事前確保、以降は buffer に直接書込 + count 引数で有効範囲指定
* 実装: `SurvivorFusionEnemyBatchSync.WriteEnemyStates(snapshots, count=-1)` オーバーロード

* 実測値（`SyncEnemyStatesAllocationPerformanceTests`）:

  | 項目 | Before（new[]） | After（buffer 再利用） | 改善率 |
  |------|---------------:|--------------------:|-------:|
  | GC Alloc / 呼出（N=500 規模） | ~20 KB | 0 B | -100% |

* 対象コード: `src/Game.Client/Assets/Programs/Runtime/MVP/Survivor/Enemy/SurvivorEnemySpawner.cs` + `src/Game.Client/Assets/Programs/Runtime/Shared/Network/Survivor/SurvivorFusionEnemyBatchSync.cs`

</details>

<details><summary>敵描画 LOD + フレーム分散再分類</summary>

* `SurvivorEnemyView.Update` での敵プロキシ更新を距離ベース 3 段階で間引く（`src/Game.Client/Assets/Programs/Runtime/MVP/Survivor/Enemy/SurvivorEnemyView.cs`）

  | ティア | 距離² 閾値 | 更新間隔 |
  |-------|----------|--------|
  | Near | < 400 (20m²) | 毎フレーム |
  | Mid | < 1600 (40m²) | 2 フレーム毎 |
  | Far | ≥ 1600 | 5 フレーム毎 |

* フレーム分散: プロキシごとに `FrameOffset = NetworkId % FarUpdateInterval` を割当、LOD 再分類タイミングを分散させて同一フレの再分類 spike を回避

* 実測値（`LodEffectivenessTests`、PlayMode 統合テスト）:

  | 敵数 | LOD OFF（Before） | LOD ON（After） | 削減率 |
  |------|-----------------:|-----------------:|-------:|
  | 300 | 実測値 | 実測値 | **59.1%** |
  | 500 | 実測値 | 実測値 | **60.1%** |

  `SurvivorEnemyView.Update` Self Time が敵数に応じてほぼ線形に削減

</details>

<details><summary>NonAlloc 物理クエリと buffer 再利用</summary>

hot path で呼出す全 `Physics.OverlapSphere` / `SphereCast` / `RaycastNonAlloc` を、固定サイズ配列 `readonly` フィールドに統一して毎フレ alloc を排除。

| 箇所 | バッファ | サイズ | 用途 |
|------|---------|------|------|
| `SurvivorAutoFireWeapon` | `_hitBuffer` | `Collider[50]` | 武器最近傍敵探索 |
| `SurvivorProjectile` | `_sphereCastHits` | `RaycastHit[10]` | 弾衝突検出 |
| `SurvivorGroundDamageArea` | `s_overlapBuffer` | `Collider[32]`（static） | ダメージエリア内敵検出 |
| `SurvivorPlayerController` | `_itemHitBuffer` | `Collider[50]` | アイテム吸引検出 |
| `SurvivorNetworkWeaponManager` | `s_pierceHitBuffer` | `RaycastHit[32]`（static） | サーバー側貫通処理 |
| `LockOnService` | `_hitBuffer` | `Collider[50]` | ロックオン候補収集 |
| `EcsEnemyProxy` | `s_overlapBuffer` | `Collider[8]`（static） | ECS 敵攻撃範囲 |
| `ScoreTimeAttackEnemyController` | `_raycastHits` / `_overlapResults` | `RaycastHit[1]` + `Collider[10]` | 視線判定・プレイヤー検知 |

合計 10 箇所、いずれも `readonly` フィールドでインスタンスに保持し、毎呼出で再利用。

</details>

<details><summary>オブジェクトプール（弾・VFX・敵・アイテム）</summary>

サバイバーゲームは毎秒数十〜数百の弾 / VFX spawn が発生する。全 spawn を Pool 化。

`WeaponObjectPool<T>` ジェネリック実装（`src/Game.Client/Assets/Programs/Runtime/MVP/Survivor/Weapon/WeaponObjectPool.cs`）:
* `Queue<T> _pool` で待機アイテム管理、`Get()` / `TryReturn()` は O(1)
* `HashSet<T> _activeItems` でアクティブ追跡、二重 Return を防止
* 初期化時に initialSize 分を pre-instantiate

適用箇所:
* 弾 (`SurvivorProjectile`) / 地面設置武器 (`SurvivorGroundWeapon`) - `WeaponObjectPool<T>`
* 敵 (`SurvivorEnemyController`) - `Dictionary<int, Queue<T>>`（敵 ID 毎）
* アイテム (`SurvivorItem`) - 同様
* VFX (`ParticleSystem`) - `Dictionary<string, Queue<T>>`（アセット名 key）
* ECS 敵プロキシ (`EcsEnemyProxy`) - 同様

</details>

---

## 使用言語/ライブラリ/ツール

**クライアント (Unity):**

| ライブラリ              | バージョン   | 用途                          |
|----------------------|------------|-------------------------------|
| Unity                | 6000.3.8f1 | ゲームエンジン                  |
| cysharp/UniTask      | 2.5.10     | 非同期処理                     |
| cysharp/R3           | 1.3.0      | リアクティブプログラミング (MVP)  |
| cysharp/MessagePipe  | 1.8.1      | Pub/Sub メッセージング (MVC)    |
| cysharp/MasterMemory | 3.0.4      | インメモリマスターデータDB       |
| cysharp/MessagePack  | 3.1.3      | バイナリシリアライズ             |
| cysharp/MemoryPack   | 1.21.3     | セーブデータシリアライズ          |
| hadashiA/VContainer  | 1.17.0     | DIコンテナ (MVP)               |
| MagicOnion.Client    | 7.0.9      | gRPC StreamingHub クライアント  |
| Photon Fusion 2      | 2.0        | リアルタイムネットワーク（Server/Client） |
| Fusion.Addons.KCC    | -          | Kinematic Character Controller  |
| Fusion.Addons.FSM    | -          | ネットワーク同期ステートマシン     |
| Unity.Dedicated Server| 1.3.2     | Dedicated Server ビルド          |
| DOTween              | 1.2.790    | アニメーション                  |

**サーバー (ASP.NET Core 9):**

| ライブラリ              | バージョン   | 用途                          |
|----------------------|------------|-------------------------------|
| .NET SDK             | 9.0        | ランタイム                     |
| MagicOnion.Server    | 7.0.9      | gRPC StreamingHub サーバー     |
| Grpc.AspNetCore      | 2.71.0     | gRPC 基盤                     |
| Dapper               | 2.1.66     | マイクロORM                    |
| Npgsql               | 9.0.3      | PostgreSQL ドライバ            |
| FluentMigrator       | 6.2.0      | DBマイグレーション               |
| StackExchange.Redis  | 2.8.41     | Valkey/Redis クライアント       |
| Serilog              | 9.0.0      | 構造化ログ                     |
| OpenTelemetry        | 1.11.2     | 分散トレーシング・メトリクス       |
| Scalar.AspNetCore    | 2.0.36     | OpenAPI ドキュメントUI          |
| BCrypt.Net-Next      | 4.0.3      | パスワードハッシュ               |

**開発ツール:**

| ツール                | バージョン   |
|----------------------|------------|
| JetBrains Rider      | 2025.3.0.2 |
| Claude Code          | -          |
| HotReload            | 1.13.13    |
| xUnit                | 2.x        |
| NSubstitute          | 5.3.0      |

---

## 主なライブラリ選定理由

| ライブラリ | 用途 | 選定理由 |
|-----------|------|---------|
| VContainer | DI コンテナ (MVP) | Zenjectより軽量、IL2CPP/ソースジェネレータ対応、起動時間短縮 |
| MessagePipe | Pub/Sub (MVC/MVP) | VContainer統合、型安全なメッセージング。高頻度イベント（衝突等）は直接呼出に変更済 |
| R3 | リアクティブ | UniRx後継（UniRxはアーカイブ済み）。ObservableTrackerによるリーク検出、DistinctUntilChanged/Merge/ThrottleFirst等の演算子合成でUI更新・ネットワーク状態監視を構築 |
| UniTask | 非同期処理 | Unity最適化のゼロアロケーションasync/await。WhenAll/WhenAny、CancellationToken伝搬、UniTask Tracker（リーク検出） |
| MasterMemory | マスターデータDB | インメモリ高速検索。Protobufスキーマ駆動でClient/Serverに同一定義から別バイナリを生成 |
| MemoryPack | セーブデータ | 高速バイナリシリアライズ。自動保存（30秒間隔）のパフォーマンス要件を満たす |
| Photon Fusion 2 | リアルタイム通信 | Server/Clientモード（サーバー権威）、[Networked]自動同期、KCC/FSMアドオン。PUN2はPhoton公式がレガシー宣言済 |
| MagicOnion | gRPC通信 | C#インターフェース共有で型安全なRPC。Unary + StreamingHub両対応。コード生成不要 |

---

## アセット
* 主にUnityAssetStoreのもので自作は含まれません
* Unityちゃん: https://unity-chan.com/ (© Unity Technologies Japan/UCL)

---

## 制作期間・規模

| 項目 | 値                                               |
|------|-------------------------------------------------|
| 制作期間 | 約14週間（2026年1月〜）                                 |
| C#ファイル数 | 447ファイル（うちテスト50）                                |
| テスト | 1,148テスト（EditMode 746 + PlayMode 63 + サーバー 339） |
| ドキュメント | 73ファイル                                          |
| CI/CDワークフロー | 7本                                              |
| カスタムシェーダー | 5シェーダー + 2 HLSLインクルード                           |
| EditorWindow | 12個                                             |
| ADR（設計判断記録）| 14件                                             |

---

## 今後の予定

**ゲームアップデート:**
* サバイバーゲームの拡張(新しい武器やアイテムの追加等)
* ゲームモード追加(ターン制ゲーム等)

**パフォーマンス・描画:**
* Unity Profiler / Memory Profilerによる計測結果のドキュメント化（CPU Timeline、GC Allocスナップショット）
* URP Renderer Featureによるカスタムポストエフェクト（アウトライン後処理等）

**UI演出:**
* DOTween Sequenceによる複合UI演出（レベルアップ、リザルト）
* スキップ・割り込み制御（DOKill / IsTweening ガード）の実装

**プラットフォーム:**
* ローカライズ対応（多言語、Unity Localization）
* マルチプラットフォーム対応（iOS / Androidビルド・署名）

**その他の機能:**
* ゲーム内オプション項目追加(ボーダーレス、マルチ解像度、マウス感度など)
* 課金システム・ガチャ・プレゼントBOXなど
* アプリ外課金Webサイトデモ(Next.js)

---

## デモゲームについて
### タイムアタック（MVC）
* 制限時間内に全3ステージに配置されたアイテムを規定数集めるタイムアタックです
* 動作環境: PC／マウス&キーボード
* 操作: 移動(WASD), ジャンプ(Space), 走る(LShift+移動), カメラ操作(マウスドラッグ)

### サバイバー（MVP）
* VContainerを用いたMVPパターンで実装
* ウェーブ制の敵を倒しながら生き残るサバイバーゲーム
* 動作環境: PC／マウス&キーボード
* 操作: 移動(WASD), ダッシュ(LShift+移動), カメラ操作(右クリック+ドラッグ), 手動発動武器(アイコンを左クリック), ロックオン(エネミーを左クリック)
* 主要機能:
  - ユーザーアカウント作成
  - データ連携(メール/PW, 引継パスワード発行)
  - オプション(セーブデータ管理, オーディオ音量調整)
  - 自動攻撃武器システム（マスターデータ駆動）
  - ウェーブ管理（敵の段階的出現）
  - アイテムドロップ・吸引
  - ステージクリア・記録データ保存
  - マルチプレイヤーロビー（作成/参加/チャット/レディ）
  - マッチメイキング（キューベース自動マッチング）
  - ゲーム終了後のロビー自動復帰

### ダウンロード
* 実行形式: [デモゲームDLリンク](https://drive.google.com/file/d/1_9vWOvT8leUjd2jB5uTzziSyA5goPmJx/view?usp=drive_link) ※解凍できない場合は7Zipを推奨
  - GameStartボタンを押下するとリモートアセットをダウンロードします(約500MB)
  - 稼働コスト削減のため、Unityサーバーが稼働していない場合があります。デモ動画をご覧ください

---

## 詳細ドキュメント
- [アーキテクチャ詳細](ARCHITECTURE.md)

---

## ライセンス
[LICENSE](LICENSE)
