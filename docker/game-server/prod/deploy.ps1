# docker/game-server/prod/deploy.ps1
# Cloud Run deployment script (PowerShell)
#
# Usage:
#   cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod
#   .\deploy.ps1

param(
    [switch]$BuildOnly,
    [switch]$SkipBuild,
    [string]$Tag = "latest"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\..\..\..\"

# Load .env file
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

# Check required variables
$RequiredVars = @("PROJECT_ID", "REGION", "REPO_NAME", "SERVICE_NAME", "INSTANCE_NAME", "SECRET_DB_CONNECTION", "SECRET_JWT", "SECRET_REQUEST_SIGNING")
foreach ($var in $RequiredVars) {
    if (-not (Get-Item -Path "Env:$var" -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Required variable $var is not set in .env" -ForegroundColor Red
        exit 1
    }
}

$IMAGE = "$env:REGION-docker.pkg.dev/$env:PROJECT_ID/$env:REPO_NAME/game-server"

# Get Cloud SQL connection name
Write-Host "[0/4] Getting Cloud SQL connection name..." -ForegroundColor Yellow
$CONNECTION_NAME = gcloud sql instances describe $env:INSTANCE_NAME --format="value(connectionName)" 2>$null
if (-not $CONNECTION_NAME) {
    Write-Host "[ERROR] Failed to get Cloud SQL connection name for instance: $env:INSTANCE_NAME" -ForegroundColor Red
    exit 1
}

# Check Valkey settings (warning only)
$ValkeyEnabled = $false
if ($env:VALKEY_HOST -and $env:VPC_CONNECTOR) {
    $ValkeyEnabled = $true
} elseif ($env:VALKEY_HOST -or $env:VPC_CONNECTOR) {
    Write-Host "[WARN] Valkey requires both VALKEY_HOST and VPC_CONNECTOR to be set." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===== Deploy Configuration =====" -ForegroundColor Cyan
Write-Host "PROJECT_ID:      $env:PROJECT_ID"
Write-Host "REGION:          $env:REGION"
Write-Host "SERVICE_NAME:    $env:SERVICE_NAME"
Write-Host "IMAGE:           ${IMAGE}:${Tag}"
Write-Host "CLOUD_SQL:       $CONNECTION_NAME"
Write-Host "DATABASE:        $env:DB_NAME"
if ($ValkeyEnabled) {
    Write-Host "VALKEY:          $env:VALKEY_HOST`:$env:VALKEY_PORT"
    Write-Host "VPC_CONNECTOR:   $env:VPC_CONNECTOR"
} else {
    Write-Host "VALKEY:          (not configured)"
}
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Docker authentication
Write-Host "[1/4] Configuring Docker authentication..." -ForegroundColor Yellow
gcloud auth configure-docker "$env:REGION-docker.pkg.dev" --quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipBuild) {
    # Docker build
    Write-Host "[2/4] Building Docker image..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    docker build -t "${IMAGE}:${Tag}" -f docker/game-server/prod/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
    Pop-Location

    # Push to registry
    Write-Host "[3/4] Pushing to Artifact Registry..." -ForegroundColor Yellow
    docker push "${IMAGE}:${Tag}"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "[2/4] Skipping build..." -ForegroundColor Gray
    Write-Host "[3/4] Skipping push..." -ForegroundColor Gray
}

if (-not $BuildOnly) {
    # Cloud Run deployment
    Write-Host "[4/4] Deploying to Cloud Run..." -ForegroundColor Yellow

    # Build environment variables (non-sensitive only)
    $EnvVars = @(
        "ASPNETCORE_ENVIRONMENT=Production"
    )

    if ($env:Jwt__Issuer) { $EnvVars += "Jwt__Issuer=$env:Jwt__Issuer" }
    if ($env:Jwt__Audience) { $EnvVars += "Jwt__Audience=$env:Jwt__Audience" }

    # Add Valkey settings if configured
    if ($ValkeyEnabled) {
        $ValkeyPort = if ($env:VALKEY_PORT) { $env:VALKEY_PORT } else { "6379" }
        $EnvVars += "ConnectionStrings__Valkey=$env:VALKEY_HOST`:${ValkeyPort},abortConnect=false,connectTimeout=5000"
    }

    $EnvVarsString = $EnvVars -join ","

    # Build Secret Manager secrets
    $Secrets = @(
        "ConnectionStrings__Default=$env:SECRET_DB_CONNECTION`:latest",
        "Jwt__Secret=$env:SECRET_JWT`:latest",
        "RequestSigning__SecretKey=$env:SECRET_REQUEST_SIGNING`:latest"
    )
    if ($env:SECRET_RESEND) { $Secrets += "Resend__ApiKey=$env:SECRET_RESEND`:latest" }
    $SecretsString = $Secrets -join ","

    # Build deploy command
    $DeployArgs = @(
        "run", "deploy", $env:SERVICE_NAME,
        "--image=${IMAGE}:${Tag}",
        "--region=$env:REGION",
        "--platform=managed",
        "--allow-unauthenticated",
        "--add-cloudsql-instances=$CONNECTION_NAME",
        "--set-env-vars=$EnvVarsString",
        "--set-secrets=$SecretsString",
        "--memory=512Mi",
        "--cpu=1",
        "--min-instances=0",
        "--max-instances=10",
        "--concurrency=80",
        "--timeout=300"
    )

    # Add VPC Connector if Valkey is enabled
    if ($ValkeyEnabled) {
        $DeployArgs += "--vpc-connector=$env:VPC_CONNECTOR"
    }

    & gcloud @DeployArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Show URL
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
