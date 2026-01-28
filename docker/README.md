# Unity GitHub Actions Self-hosted Runner (Docker)

Docker コンテナ上で GitHub Actions Self-hosted Runner を実行するための設定ファイルです。
GitHub App を使用した認証を行います。

## 認証方式

**GitHub App** による認証（推奨）
- より細かい権限制御が可能
- ユーザーに紐づかないため、担当者変更時も影響なし
- 監査ログでアプリアクションとして記録

> **Note**: PAT (Personal Access Token) 認証を使用する場合は `backup/` フォルダの設定を参照してください。

## 対応プラットフォーム

| ホストOS | 対応状況 | 備考 |
|---------|---------|------|
| Linux (Ubuntu) | 推奨 | ネイティブサポート |
| Windows (Docker Desktop + WSL2) | 対応 | WSL2バックエンド必須 |
| macOS (Docker Desktop) | 未検証 | 動作する可能性あり |

## ビルドプラットフォーム選択

`.env` ファイルで `UNITY_MODULE` を設定することで、ビルドターゲットを変更できます。

| UNITY_MODULE | 用途 | 備考 |
|--------------|------|------|
| `windows-mono` | Windows ビルド | **デフォルト** |
| `linux-il2cpp` | Linux ビルド | IL2CPP 対応 |
| `mac-mono` | macOS ビルド | Mono 対応 |
| `webgl` | WebGL ビルド | |
| `android` | Android ビルド | |
| `ios` | iOS ビルド | Xcode プロジェクト出力のみ |

### 設定例

```bash
# .env ファイル
UNITY_VERSION=6000.0.34f1
UNITY_MODULE=windows-mono   # ここを変更
IMAGE_VERSION=3
```

## クイックスタート (Linux)

```bash
# 1. 環境変数を設定
cp .env.example .env
# .env ファイルを編集
# - GITHUB_REPOSITORY を設定
# - GITHUB_APP_ID, GITHUB_APP_INSTALLATION_ID を設定
# - GITHUB_APP_PRIVATE_KEY_BASE64 を設定

# 秘密鍵を Base64 エンコード
cat private-key.pem | base64 -w 0

# 2. Unity ライセンスを配置
sudo mkdir -p /var/lib/unity-license
sudo cp /path/to/Unity_v6000.x.ulf /var/lib/unity-license/

# 3. イメージをビルドして起動
docker compose build
docker compose up -d

# 4. ログを確認
docker compose logs -f
```

## クイックスタート (Windows)

```powershell
# 1. 環境変数を設定
copy .env.example .env
# .env ファイルを編集
# - GITHUB_REPOSITORY を設定
# - GITHUB_APP_ID, GITHUB_APP_INSTALLATION_ID を設定
# - GITHUB_APP_PRIVATE_KEY_BASE64 を設定
# - UNITY_LICENSE_PATH=C:/unity-license に変更

# 秘密鍵を Base64 エンコード
[Convert]::ToBase64String([IO.File]::ReadAllBytes("private-key.pem"))

# 2. Unity ライセンスを配置
mkdir C:\unity-license
copy "C:\path\to\Unity_v6000.x.ulf" "C:\unity-license\"

# 3. イメージをビルドして起動
docker compose build
docker compose up -d

# 4. ログを確認
docker compose logs -f
```

## 複数プラットフォーム対応

複数のビルドターゲットが必要な場合は、各プラットフォーム用に別々のコンテナを起動してください：

```bash
# Windows ビルド用
UNITY_MODULE=windows-mono docker compose up -d

# WebGL ビルド用（別ターミナル or 別ディレクトリ）
UNITY_MODULE=webgl docker compose -p unity-webgl up -d
```

## ファイル構成

```
docker/
├── Dockerfile              # Runner イメージ定義
├── docker-compose.yml      # Docker Compose 設定
├── .env.example            # 環境変数テンプレート
├── README.md               # このファイル
├── scripts/
│   ├── entrypoint.sh       # コンテナ起動スクリプト
│   ├── github-app-token.sh # GitHub App トークン生成
│   └── cleanup.sh          # Runner 登録解除スクリプト
└── backup/                 # PAT認証版（非推奨）
    ├── Dockerfile.pat
    ├── docker-compose.pat.yml
    ├── docker-compose.windows.pat.yml
    └── entrypoint.pat.sh
```

## 詳細ドキュメント

- [GITHUB_ACTIONS_DOCKER_SETUP.md](../docs/GITHUB_ACTIONS_DOCKER_SETUP.md) - Docker Runner セットアップ
- [WORKFLOW_TRIGGER_GUIDE.md](../docs/WORKFLOW_TRIGGER_GUIDE.md) - ワークフロー実行ガイド
- [GITHUB_AUTH_SETUP.md](../docs/GITHUB_AUTH_SETUP.md) - GitHub 認証設定

## 注意事項

- `.env` ファイルには機密情報が含まれるため、**絶対にコミットしないでください**
- Unity ライセンスファイルも同様にコミットしないでください
