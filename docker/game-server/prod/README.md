# Game Server Production (Cloud Run)

Google Cloud Run 用の本番環境デプロイ設定。

## ファイル構成

| ファイル | 説明 |
|---------|------|
| `Dockerfile` | Cloud Run 用マルチステージビルド |
| `docker-compose.yml` | ローカルテスト用（Cloud Run では不使用） |
| `cloudbuild.yml` | Cloud Build デプロイ設定 |
| `.env.example` | 環境変数テンプレート |
| `.env` | 環境変数（Git 管理外） |
| `deploy.ps1` | デプロイスクリプト（PowerShell） |
| `deploy.sh` | デプロイスクリプト（bash） |
| `db.ps1` | データベース管理ツール（PowerShell） |
| `db.sh` | データベース管理ツール（bash） |

## セットアップ

### 1. 環境変数の設定

```powershell
cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod

# テンプレートをコピー
cp .env.example .env

# .env を編集して値を設定
notepad .env
```

`.env` の設定例：

```env
PROJECT_ID=my-game-project-12345
REGION=asia-northeast1
REPO_NAME=game-server
INSTANCE_NAME=game-db
DB_NAME=gamedb
DB_USER=gameserver
DB_PASSWORD=secure-password-here
SERVICE_NAME=game-server
JWT_SECRET_KEY=your-32-char-minimum-secret-key
JWT_ISSUER=GameServer
JWT_AUDIENCE=GameClient
```

## デプロイ方法

### 方法1: デプロイスクリプト（推奨）

**PowerShell:**

```powershell
cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod

# フルデプロイ（ビルド + プッシュ + デプロイ）
.\deploy.ps1

# ビルドのみ
.\deploy.ps1 -BuildOnly

# デプロイのみ（既存イメージを使用）
.\deploy.ps1 -SkipBuild

# タグ指定
.\deploy.ps1 -Tag "v1.0.0"
```

**bash (WSL2/macOS/Linux):**

```bash
cd Unity6Portfolio/docker/game-server/prod

# 実行権限付与
chmod +x deploy.sh

# フルデプロイ
./deploy.sh

# ビルドのみ
./deploy.sh --build-only

# デプロイのみ
./deploy.sh --skip-build

# タグ指定
./deploy.sh --tag v1.0.0
```

### 方法2: Cloud Build

```powershell
cd E:\UnityProjects\Unity6Portfolio

# .env から環境変数を読み込んでから実行
gcloud builds submit --config=docker/game-server/prod/cloudbuild.yml `
  --substitutions=_REGION=$env:REGION,_REPO_NAME=$env:REPO_NAME
```

### 方法3: 手動コマンド

```powershell
# .env を読み込む（PowerShell）
Get-Content docker\game-server\prod\.env | ForEach-Object {
    if ($_ -match '^([^#][^=]+)=(.*)$') {
        Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim()
    }
}

# ビルド & デプロイ
$IMAGE = "$env:REGION-docker.pkg.dev/$env:PROJECT_ID/$env:REPO_NAME/game-server"
docker build -t "${IMAGE}:latest" -f docker/game-server/prod/Dockerfile .
docker push "${IMAGE}:latest"
gcloud run deploy $env:SERVICE_NAME --image="${IMAGE}:latest" --region=$env:REGION
```

## ローカルテスト

```powershell
cd E:\UnityProjects\Unity6Portfolio

# PostgreSQL + サーバーを起動
docker compose -f docker/game-server/prod/docker-compose.yml up --build

# ヘルスチェック
curl http://localhost:8080/health
```

## Cloud SQL 接続設定

Cloud Run デプロイ時に Cloud SQL を接続：

```powershell
# 接続名を取得
$CONNECTION_NAME = gcloud sql instances describe $env:INSTANCE_NAME --format="value(connectionName)"

# Cloud SQL 付きでデプロイ
gcloud run deploy $env:SERVICE_NAME `
  --image="${IMAGE}:latest" `
  --region=$env:REGION `
  --add-cloudsql-instances=$CONNECTION_NAME `
  --set-env-vars="ConnectionStrings__Default=Host=/cloudsql/$CONNECTION_NAME;Database=$env:DB_NAME;Username=$env:DB_USER;Password=$env:DB_PASSWORD"
```

## データベース管理ツール

Cloud SQL へのマイグレーション、シードデータ適用を簡単に行えるツールです。

### クイックスタート

```powershell
cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod

# 1. 別ターミナルで Proxy を起動（起動したままにする）
.\db.ps1 proxy

# 2. マイグレーション実行
.\db.ps1 migrate

# 3. シードデータ適用
.\db.ps1 seed
```

### 利用可能なコマンド

| コマンド | 説明 |
|---------|------|
| `.\db.ps1 proxy` | Cloud SQL Auth Proxy を起動（最初に実行） |
| `.\db.ps1 migrate` | 保留中のマイグレーションを実行 |
| `.\db.ps1 seed` | TSV ファイルからシードデータを適用 |
| `.\db.ps1 status` | マイグレーション状態を表示 |
| `.\db.ps1 reset` | データベースをリセット（注意：全データ削除） |
| `.\db.ps1 dump` | データベースを TSV ファイルにダンプ |

### オプション

```powershell
# スキーマを指定（master, user, all）
.\db.ps1 migrate -Schema master

# リセット時に確認をスキップ
.\db.ps1 reset -Force

# リセット後にシードも実行
.\db.ps1 reset -Force -WithSeed

# Proxy ポートを変更（デフォルト: 5433）
.\db.ps1 proxy -ProxyPort 5434
```

### 使用例：初回セットアップ

```powershell
# ターミナル1: Proxy を起動
.\db.ps1 proxy

# ターミナル2: マイグレーションとシード
.\db.ps1 migrate
.\db.ps1 seed
```

### 使用例：データベースリセット

```powershell
# 全テーブルを削除して再作成、シードデータも適用
.\db.ps1 reset -Force -WithSeed
```

### bash 版（WSL2/macOS/Linux）

```bash
cd Unity6Portfolio/docker/game-server/prod
chmod +x db.sh

./db.sh proxy
./db.sh migrate
./db.sh seed
./db.sh reset --force --with-seed
```

---

## コマンドベースでのデータベース操作

`db.ps1` を使用せず、直接コマンドで操作する方法です。

### 前提条件

1. Cloud SQL Auth Proxy がダウンロード済み
2. gcloud 認証済み（`gcloud auth application-default login`）

### Step 1: Cloud SQL Auth Proxy を起動

**ターミナル1（起動したままにする）:**

```powershell
cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod

# Proxy をダウンロード（初回のみ）
curl -o cloud-sql-proxy.exe https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.15.0/cloud-sql-proxy.x64.exe

# 接続名を取得
$CONNECTION_NAME = gcloud sql instances describe game-db --format="value(connectionName)"
# 出力例: game-server-prod-20260210:asia-northeast1:game-db

# Proxy を起動（ポート 5433）
.\cloud-sql-proxy.exe $CONNECTION_NAME --port=5433
```

### Step 2: 接続文字列を設定

**ターミナル2:**

```powershell
# 接続文字列を環境変数に設定
$env:CONNECTION_STRING = "Host=localhost;Port=5433;Database=gamedb;Username=gameserver;Password=YOUR_PASSWORD"

# プロジェクトルートに移動
cd E:\UnityProjects\Unity6Portfolio
```

### Step 3: マイグレーション実行

```powershell
# マイグレーション状態を確認
dotnet run --project src/Game.Tools -- migrate status --connection-string $env:CONNECTION_STRING

# 全スキーマのマイグレーションを実行
dotnet run --project src/Game.Tools -- migrate up --connection-string $env:CONNECTION_STRING

# 特定スキーマのみ実行
dotnet run --project src/Game.Tools -- migrate up --connection-string $env:CONNECTION_STRING --schema master
dotnet run --project src/Game.Tools -- migrate up --connection-string $env:CONNECTION_STRING --schema user
```

### Step 4: シードデータ適用

```powershell
# マスターデータをシード（masterdata/raw/ の TSV ファイルから）
dotnet run --project src/Game.Tools -- seeddata seed --connection-string $env:CONNECTION_STRING

# 特定スキーマのみ
dotnet run --project src/Game.Tools -- seeddata seed --connection-string $env:CONNECTION_STRING --schema master
```

### Step 5: データベースリセット（注意）

```powershell
# データベースをリセット（全テーブル削除 → 再作成 → シード）
dotnet run --project src/Game.Tools -- migrate reset --connection-string $env:CONNECTION_STRING --force --seed --version 999999999999

# シードなしでリセット
dotnet run --project src/Game.Tools -- migrate reset --connection-string $env:CONNECTION_STRING --force --version 999999999999
```

### Step 6: データダンプ

```powershell
# データベースの内容を TSV ファイルにダンプ
dotnet run --project src/Game.Tools -- seeddata dump --connection-string $env:CONNECTION_STRING --out-dir masterdata/dump/
```

### Game.Tools コマンドリファレンス

| コマンド | 説明 |
|---------|------|
| `migrate up` | 保留中のマイグレーションを実行 |
| `migrate down` | マイグレーションをロールバック |
| `migrate status` | マイグレーション状態を表示 |
| `migrate reset` | スキーマを削除して再作成 |
| `seeddata seed` | TSV ファイルからデータを投入 |
| `seeddata dump` | データベースを TSV にエクスポート |

### オプション一覧

| オプション | 説明 | 例 |
|-----------|------|-----|
| `--connection-string` | PostgreSQL 接続文字列 | `Host=localhost;Port=5433;...` |
| `--schema` | 対象スキーマ（master, user, all） | `--schema master` |
| `--force` | 確認プロンプトをスキップ | `--force` |
| `--seed` | リセット後にシードを実行 | `--seed` |
| `--version` | マイグレーションバージョン | `--version 999999999999` |
| `--tsv-dir` | TSV ファイルのディレクトリ | `--tsv-dir masterdata/raw/` |
| `--out-dir` | 出力ディレクトリ | `--out-dir masterdata/dump/` |

### bash 版コマンド例

```bash
cd Unity6Portfolio

# 接続文字列
export CONNECTION_STRING="Host=localhost;Port=5433;Database=gamedb;Username=gameserver;Password=YOUR_PASSWORD"

# マイグレーション
dotnet run --project src/Game.Tools -- migrate up --connection-string "$CONNECTION_STRING"

# シード
dotnet run --project src/Game.Tools -- seeddata seed --connection-string "$CONNECTION_STRING"

# リセット
dotnet run --project src/Game.Tools -- migrate reset --connection-string "$CONNECTION_STRING" --force --seed --version 999999999999
```

---

## DataGrip から Cloud SQL に接続

### 方法1: Cloud SQL Auth Proxy（推奨）

最もセキュアな方法です。パブリック IP への直接接続を許可する必要がありません。

#### 1. Cloud SQL Auth Proxy のダウンロード

```powershell
# Windows
curl -o cloud-sql-proxy.exe https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.15.0/cloud-sql-proxy.x64.exe
```

#### 2. gcloud 認証

```powershell
gcloud auth application-default login
```

#### 3. Proxy を起動

```powershell
# 接続名を確認
gcloud sql instances describe game-db --format="value(connectionName)"
# 出力例: game-server-prod-20260210:asia-northeast1:game-db

# Proxy を起動（ポート 5433 でリッスン）
.\cloud-sql-proxy.exe "game-server-prod-20260210:asia-northeast1:game-db" --port=5433
```

#### 4. DataGrip の設定

1. **Database** > **New** > **Data Source** > **PostgreSQL**
2. 以下を設定：

| 項目 | 値 |
|------|-----|
| Host | `localhost` |
| Port | `5433` |
| Database | `gamedb` |
| User | `gameserver` |
| Password | `.env` の `DB_PASSWORD` |

3. **Test Connection** で接続確認
4. **OK** で保存

---

### 方法2: パブリック IP 接続

Cloud SQL のパブリック IP に直接接続する方法です。IP を許可リストに追加する必要があります。

#### 1. 自分の IP を許可リストに追加

```powershell
# 現在の IP を取得
$MY_IP = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content

# Cloud SQL に IP を追加
gcloud sql instances patch game-db --authorized-networks=$MY_IP/32
```

> **注意:** 動的 IP の場合、IP が変わるたびに再設定が必要です。

#### 2. Cloud SQL のパブリック IP を確認

```powershell
gcloud sql instances describe game-db --format="value(ipAddresses[0].ipAddress)"
# 出力例: 34.146.155.150
```

#### 3. DataGrip の設定

1. **Database** > **New** > **Data Source** > **PostgreSQL**
2. 以下を設定：

| 項目 | 値 |
|------|-----|
| Host | `34.146.155.150`（上記で取得した IP） |
| Port | `5432` |
| Database | `gamedb` |
| User | `gameserver` |
| Password | `.env` の `DB_PASSWORD` |
| SSL | `require`（推奨） |

3. **Test Connection** で接続確認
4. **OK** で保存

---

### 接続情報まとめ

| 項目 | Auth Proxy 経由 | パブリック IP 直接 |
|------|----------------|-------------------|
| Host | `localhost` | Cloud SQL の IP |
| Port | `5433`（Proxy 設定） | `5432` |
| Database | `gamedb` | `gamedb` |
| User | `gameserver` | `gameserver` |
| Password | `.env` の `DB_PASSWORD` | `.env` の `DB_PASSWORD` |
| セキュリティ | ✅ 高（IP 許可不要） | ⚠️ IP 許可が必要 |

---

## 関連ドキュメント

- [Cloud Run デプロイメントガイド](../../../docs/deployment/CLOUD_RUN_DEPLOYMENT_GUIDE.md)
- [Fly.io デプロイメントガイド](../../../docs/deployment/FLY_IO_DEPLOYMENT_GUIDE.md)
