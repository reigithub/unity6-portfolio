# docker/game-server/prod/deploy.ps1
# Cloud Run デプロイスクリプト（PowerShell）
#
# 使用方法:
#   cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod
#   .\deploy.ps1

param(
    [switch]$BuildOnly,      # ビルドのみ（デプロイしない）
    [switch]$SkipBuild,      # ビルドをスキップ（デプロイのみ）
    [string]$Tag = "latest"  # イメージタグ
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\..\..\..\"

# .env ファイルを読み込み
$EnvFile = "$ScriptDir\.env"
if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            if ($value -ne '') {
                Set-Item -Path "Env:$name" -Value $value
            }
        }
    }
    Write-Host "[OK] .env loaded" -ForegroundColor Green
} else {
    Write-Host "[ERROR] .env file not found. Copy .env.example to .env and configure." -ForegroundColor Red
    exit 1
}

# 必須変数の確認
$RequiredVars = @("PROJECT_ID", "REGION", "REPO_NAME", "SERVICE_NAME", "INSTANCE_NAME", "DB_NAME", "DB_USER", "DB_PASSWORD")
foreach ($var in $RequiredVars) {
    if (-not (Get-Item -Path "Env:$var" -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Required variable $var is not set in .env" -ForegroundColor Red
        exit 1
    }
}

# JWT 設定の確認（警告のみ）
if (-not $env:Jwt__Secret) {
    Write-Host "[WARN] Jwt__Secret is not set. JWT authentication may fail." -ForegroundColor Yellow
}

$IMAGE = "$env:REGION-docker.pkg.dev/$env:PROJECT_ID/$env:REPO_NAME/game-server"

# Cloud SQL 接続名を取得
Write-Host "[0/4] Getting Cloud SQL connection name..." -ForegroundColor Yellow
$CONNECTION_NAME = gcloud sql instances describe $env:INSTANCE_NAME --format="value(connectionName)" 2>$null
if (-not $CONNECTION_NAME) {
    Write-Host "[ERROR] Failed to get Cloud SQL connection name for instance: $env:INSTANCE_NAME" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "===== Deploy Configuration =====" -ForegroundColor Cyan
Write-Host "PROJECT_ID:      $env:PROJECT_ID"
Write-Host "REGION:          $env:REGION"
Write-Host "SERVICE_NAME:    $env:SERVICE_NAME"
Write-Host "IMAGE:           ${IMAGE}:${Tag}"
Write-Host "CLOUD_SQL:       $CONNECTION_NAME"
Write-Host "DATABASE:        $env:DB_NAME"
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Docker 認証
Write-Host "[1/4] Configuring Docker authentication..." -ForegroundColor Yellow
gcloud auth configure-docker "$env:REGION-docker.pkg.dev" --quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipBuild) {
    # Docker ビルド
    Write-Host "[2/4] Building Docker image..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    docker build -t "${IMAGE}:${Tag}" -f docker/game-server/prod/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
    Pop-Location

    # プッシュ
    Write-Host "[3/4] Pushing to Artifact Registry..." -ForegroundColor Yellow
    docker push "${IMAGE}:${Tag}"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "[2/4] Skipping build..." -ForegroundColor Gray
    Write-Host "[3/4] Skipping push..." -ForegroundColor Gray
}

if (-not $BuildOnly) {
    # Cloud Run デプロイ
    Write-Host "[4/4] Deploying to Cloud Run..." -ForegroundColor Yellow

    # 接続文字列を構築
    $ConnectionString = "Host=/cloudsql/$CONNECTION_NAME;Database=$env:DB_NAME;Username=$env:DB_USER;Password=$env:DB_PASSWORD"

    # 環境変数を構築
    $EnvVars = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ConnectionStrings__Default=$ConnectionString"
    )

    # JWT 設定を追加（設定されている場合）
    if ($env:Jwt__Secret) { $EnvVars += "Jwt__Secret=$env:Jwt__Secret" }
    if ($env:Jwt__Issuer) { $EnvVars += "Jwt__Issuer=$env:Jwt__Issuer" }
    if ($env:Jwt__Audience) { $EnvVars += "Jwt__Audience=$env:Jwt__Audience" }

    # Resend 設定を追加（設定されている場合）
    if ($env:Resend__ApiKey) { $EnvVars += "Resend__ApiKey=$env:Resend__ApiKey" }

    $EnvVarsString = $EnvVars -join ","

    gcloud run deploy $env:SERVICE_NAME `
        --image="${IMAGE}:${Tag}" `
        --region=$env:REGION `
        --platform=managed `
        --allow-unauthenticated `
        --add-cloudsql-instances=$CONNECTION_NAME `
        --set-env-vars="$EnvVarsString" `
        --memory=512Mi `
        --cpu=1 `
        --min-instances=0 `
        --max-instances=10 `
        --concurrency=80 `
        --timeout=300
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # URL 表示
    Write-Host ""
    Write-Host "===== Deploy Complete =====" -ForegroundColor Green
    $url = gcloud run services describe $env:SERVICE_NAME --region=$env:REGION --format="value(status.url)"
    Write-Host "Service URL: $url" -ForegroundColor Cyan
} else {
    Write-Host "[4/4] Skipping deploy (BuildOnly mode)..." -ForegroundColor Gray
    Write-Host ""
    Write-Host "===== Build Complete =====" -ForegroundColor Green
    Write-Host "Image: ${IMAGE}:${Tag}" -ForegroundColor Cyan
}
