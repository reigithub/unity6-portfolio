# Addressables アップロードスクリプト

Unity Addressables のビルドを Cloudflare R2 にアップロードするためのスクリプトです。

## 前提条件

### 1. rclone のインストール

```powershell
# Windows (winget)
winget install Rclone.Rclone

# Windows (Scoop)
scoop install rclone
```

### 2. rclone の設定

```bash
rclone config
```

設定値:
- name: `r2`
- type: `s3`
- provider: `Cloudflare`
- access_key_id: `[R2 API トークンの Access Key ID]`
- secret_access_key: `[R2 API トークンの Secret Access Key]`
- endpoint: `https://[ACCOUNT_ID].r2.cloudflarestorage.com`

### 3. 設定の確認

```bash
# バケット一覧を表示
rclone lsd r2:

# ファイル一覧を表示
rclone ls r2:unity6-portfolio
```

## 使用方法

### コマンドラインから

```powershell
# 全プラットフォームをアップロード
.\upload-to-r2.ps1

# 特定のプラットフォームのみ
.\upload-to-r2.ps1 -Platform StandaloneWindows64

# ドライラン（実際にはアップロードしない）
.\upload-to-r2.ps1 -DryRun

# 詳細ログ
.\upload-to-r2.ps1 -VerboseOutput
```

### バッチファイルから

```cmd
REM 全プラットフォーム
upload-to-r2.bat

REM 特定のプラットフォーム
upload-to-r2.bat StandaloneWindows64

REM ドライラン
upload-to-r2.bat --dry-run
```

### Unity Editor から

1. **Build > Addressables > Build Only**
   - Addressables をビルドのみ

2. **Build > Addressables > Build and Upload to R2**
   - ビルドしてR2にアップロード

3. **Build > Addressables > Upload to R2 (Without Build)**
   - 既存のビルドをアップロード

4. **Build > Addressables > Upload to R2 (Dry Run)**
   - ドライラン（確認用）

## 設定

### スクリプト内の設定（upload-to-r2.ps1）

```powershell
$BucketName = "unity6-portfolio"  # R2 バケット名
$RcloneRemote = "r2"              # rclone リモート名
$Transfers = 8                     # 並列転送数
$Checkers = 16                     # 並列チェック数
```

### Unity Editor 内の設定（AddressablesR2Uploader.cs）

```csharp
private const string BucketName = "unity6-portfolio";
private const string RcloneRemote = "r2";
private const string CustomDomain = "rei-unity6-portfolio.com";
```

## トラブルシューティング

### rclone が見つからない

```
ERROR: rclone が見つかりません。
```

→ rclone をインストールしてください:
```powershell
winget install Rclone.Rclone
```

### リモート 'r2' が設定されていない

```
ERROR: rclone リモート 'r2' が設定されていません。
```

→ `rclone config` で R2 の設定を行ってください。

### アクセス拒否エラー

```
AccessDenied
```

→ API トークンの権限を確認してください（Object Read & Write が必要）

### ビルドパスが存在しない

```
ERROR: ビルドパスが存在しません
```

→ Unity Editor で Addressables をビルドしてから再度実行してください。

## CI/CD 環境での使用

### GitHub Actions

GitHub Actions から Addressables をビルドして R2 にデプロイできます。

#### 必要なシークレット

GitHub リポジトリに以下のシークレットを設定してください:

| シークレット名 | 説明 | 取得方法 |
|--------------|------|---------|
| `R2_ACCOUNT_ID` | Cloudflare Account ID | Dashboard 右側に表示 |
| `R2_ACCESS_KEY_ID` | R2 API Token の Access Key ID | R2 > Manage API Tokens |
| `R2_SECRET_ACCESS_KEY` | R2 API Token の Secret Access Key | トークン作成時のみ表示 |

#### ワークフロー実行

```bash
# 特定プラットフォームをビルド＆デプロイ
gh workflow run "Addressables Deploy" \
  --field build_target=StandaloneWindows64 \
  --field deploy=true

# 全プラットフォームをビルド＆デプロイ
gh workflow run "Addressables Deploy" \
  --field build_target=All \
  --field deploy=true

# ドライラン（アップロードしない）
gh workflow run "Addressables Deploy" \
  --field build_target=StandaloneWindows64 \
  --field deploy=true \
  --field dry_run=true

# ビルドのみ（デプロイしない）
gh workflow run "Addressables Deploy" \
  --field build_target=StandaloneWindows64 \
  --field deploy=false
```

#### 環境変数

CI ビルド時に以下の環境変数でカスタマイズ可能:

| 環境変数 | 説明 | デフォルト |
|---------|------|-----------|
| `ADDRESSABLES_PROFILE` | 使用するプロファイル名 | `Default` |
| `ADDRESSABLES_REMOTE_LOAD_PATH` | Remote.LoadPath のオーバーライド | (プロファイル設定を使用) |

### ローカルでの CI ビルドテスト

```bash
# Unity をバッチモードで実行
unity-editor \
  -batchmode \
  -nographics \
  -quit \
  -projectPath ./src/Game.Client \
  -buildTarget Win64 \
  -executeMethod Game.Editor.Build.AddressablesR2Uploader.BuildAddressablesCI \
  -logFile ./build.log
```

## 関連ドキュメント

- [docs/CLOUDFLARE_R2_IMPLEMENTATION.md](../../docs/CLOUDFLARE_R2_IMPLEMENTATION.md)
- [docs/ADDRESSABLES_CDN_GUIDE.md](../../docs/ADDRESSABLES_CDN_GUIDE.md)
