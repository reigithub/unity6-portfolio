<#
.SYNOPSIS
    Unity AddressablesのビルドをCloudflare R2にアップロード

.DESCRIPTION
    rcloneを使用してServerDataフォルダのAddressablesビルドをR2バケットに同期します。
    プラットフォームごとにフォルダが分かれている構造に対応しています。

.PARAMETER BuildPath
    Addressablesのビルド出力パス（デフォルト: ServerData）

.PARAMETER BucketName
    R2バケット名（デフォルト: unity6-portfolio）

.PARAMETER Platform
    アップロードするプラットフォーム（省略時は存在する全プラットフォーム）

.PARAMETER DryRun
    実際にはアップロードせず、実行内容を表示

.PARAMETER Verbose
    詳細なログを表示

.EXAMPLE
    .\upload-to-r2.ps1
    # 全プラットフォームをアップロード

.EXAMPLE
    .\upload-to-r2.ps1 -Platform StandaloneWindows64
    # Windows64のみアップロード

.EXAMPLE
    .\upload-to-r2.ps1 -DryRun
    # ドライラン（実際にはアップロードしない）

.EXAMPLE
    .\upload-to-r2.ps1 -Platform Android -Verbose
    # Androidを詳細ログ付きでアップロード
#>

param(
    [string]$BuildPath = "ServerData",
    [string]$BucketName = "unity6-portfolio",
    [string]$Platform = "",
    [switch]$DryRun = $false,
    [switch]$VerboseOutput = $false
)

# 設定
$RcloneRemote = "r2"
$Transfers = 8
$Checkers = 16

# プラットフォーム一覧
$AllPlatforms = @(
    "StandaloneWindows64",
    "StandaloneOSX",
    "StandaloneLinux64",
    "Android",
    "iOS",
    "WebGL"
)

# スクリプトのディレクトリを取得
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$ClientPath = Join-Path $ProjectRoot "src\Game.Client"
$FullBuildPath = Join-Path $ClientPath $BuildPath

# アップロード対象のプラットフォームを決定
if ($Platform -ne "") {
    $TargetPlatforms = @($Platform)
} else {
    $TargetPlatforms = $AllPlatforms
}

# rcloneが利用可能か確認
if (-not (Get-Command rclone -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: rclone が見つかりません。" -ForegroundColor Red
    Write-Host ""
    Write-Host "インストール方法:" -ForegroundColor Yellow
    Write-Host "  winget install Rclone.Rclone"
    Write-Host "  または"
    Write-Host "  scoop install rclone"
    Write-Host ""
    Write-Host "インストール後、rclone config で R2 の設定を行ってください。"
    exit 1
}

# リモート設定の確認
$remotes = rclone listremotes 2>&1
if ($remotes -notcontains "${RcloneRemote}:") {
    Write-Host "ERROR: rclone リモート '$RcloneRemote' が設定されていません。" -ForegroundColor Red
    Write-Host ""
    Write-Host "設定方法:" -ForegroundColor Yellow
    Write-Host "  rclone config"
    Write-Host ""
    Write-Host "設定値:"
    Write-Host "  name: r2"
    Write-Host "  type: s3"
    Write-Host "  provider: Cloudflare"
    Write-Host "  access_key_id: [APIトークンのAccess Key ID]"
    Write-Host "  secret_access_key: [APIトークンのSecret Access Key]"
    Write-Host "  endpoint: https://[ACCOUNT_ID].r2.cloudflarestorage.com"
    exit 1
}

# ビルドパスの確認
if (-not (Test-Path $FullBuildPath)) {
    Write-Host "ERROR: ビルドパスが存在しません: $FullBuildPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Addressablesをビルドしてから再度実行してください。"
    Write-Host "Unity Editor: Build > Addressables > Build"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cloudflare R2 アップロード" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Bucket:     $BucketName"
Write-Host "Build Path: $FullBuildPath"
Write-Host "Remote:     ${RcloneRemote}:${BucketName}"
if ($DryRun) {
    Write-Host "Mode:       DRY RUN (実際にはアップロードしません)" -ForegroundColor Yellow
}
Write-Host ""

$uploadedCount = 0
$skippedCount = 0
$failedCount = 0
$totalFiles = 0
$totalSize = 0

foreach ($platform in $TargetPlatforms) {
    $sourcePath = Join-Path $FullBuildPath $platform

    if (-not (Test-Path $sourcePath)) {
        if ($VerboseOutput) {
            Write-Host "[$platform] スキップ - ビルドが存在しません" -ForegroundColor Yellow
        }
        $skippedCount++
        continue
    }

    $files = Get-ChildItem -Path $sourcePath -File -Recurse
    $fileCount = $files.Count
    $folderSize = ($files | Measure-Object -Property Length -Sum).Sum
    $folderSizeMB = [math]::Round($folderSize / 1MB, 2)

    Write-Host "[$platform] アップロード中..." -ForegroundColor Green
    Write-Host "  Files: $fileCount, Size: ${folderSizeMB}MB"

    $destination = "${RcloneRemote}:${BucketName}/${platform}"

    $rcloneArgs = @(
        "sync",
        $sourcePath,
        $destination,
        "--transfers", $Transfers,
        "--checkers", $Checkers,
        "--progress"
    )

    if ($VerboseOutput) {
        $rcloneArgs += "--verbose"
    } else {
        $rcloneArgs += "--stats-one-line"
        $rcloneArgs += "--stats"
        $rcloneArgs += "5s"
    }

    if ($DryRun) {
        $rcloneArgs += "--dry-run"
    }

    if ($VerboseOutput) {
        Write-Host "  Command: rclone $($rcloneArgs -join ' ')" -ForegroundColor DarkGray
    }

    & rclone @rcloneArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[$platform] 完了 ✓" -ForegroundColor Green
        $uploadedCount++
        $totalFiles += $fileCount
        $totalSize += $folderSize
    } else {
        Write-Host "[$platform] エラー ✗ (Exit code: $LASTEXITCODE)" -ForegroundColor Red
        $failedCount++
    }

    Write-Host ""
}

$totalSizeMB = [math]::Round($totalSize / 1MB, 2)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 結果" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  成功:     $uploadedCount プラットフォーム" -ForegroundColor Green
Write-Host "  スキップ: $skippedCount プラットフォーム" -ForegroundColor Yellow
Write-Host "  失敗:     $failedCount プラットフォーム" -ForegroundColor $(if ($failedCount -gt 0) { "Red" } else { "White" })
Write-Host ""
Write-Host "  ファイル数: $totalFiles"
Write-Host "  合計サイズ: ${totalSizeMB}MB"
Write-Host ""

# URLを表示
if ($uploadedCount -gt 0 -and -not $DryRun) {
    Write-Host "アップロード先URL:" -ForegroundColor Yellow
    foreach ($platform in $TargetPlatforms) {
        $sourcePath = Join-Path $FullBuildPath $platform
        if (Test-Path $sourcePath) {
            Write-Host "  https://rei-unity6-portfolio.com/$platform/"
        }
    }
    Write-Host ""
}

if ($failedCount -gt 0) {
    exit 1
}

exit 0
