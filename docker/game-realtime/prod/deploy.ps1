# docker/game-realtime/prod/deploy.ps1
# Game.Realtime Cloud Run deployment script (PowerShell)
#
# Usage:
#   cd E:\UnityProjects\Unity6Portfolio\docker\game-realtime\prod
#   .\deploy.ps1
#
# Cloud Run settings (differences from Game.Server):
#   - No Cloud SQL (Valkey only)
#   - min-instances=1 (StreamingHub persistent connections)
#   - session-affinity enabled (StreamingHub sticky sessions)
#   - HTTP/2 enabled (gRPC)

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

# Check required variables (no Cloud SQL needed)
$RequiredVars = @("PROJECT_ID", "REGION", "REPO_NAME", "SERVICE_NAME")
foreach ($var in $RequiredVars) {
    if (-not (Get-Item -Path "Env:$var" -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Required variable $var is not set in .env" -ForegroundColor Red
        exit 1
    }
}

# Check JWT settings (warning only)
if (-not $env:Jwt__Secret) {
    Write-Host "[WARN] Jwt__Secret is not set. JWT authentication may fail." -ForegroundColor Yellow
}

# Check Valkey settings (critical for Game.Realtime)
$ValkeyEnabled = $false
if ($env:VALKEY_HOST -and $env:VPC_NETWORK -and $env:VPC_SUBNET) {
    $ValkeyEnabled = $true
} elseif ($env:VALKEY_HOST) {
    Write-Host "[WARN] Valkey requires VALKEY_HOST, VPC_NETWORK, and VPC_SUBNET to be set." -ForegroundColor Yellow
}
if (-not $ValkeyEnabled) {
    Write-Host "[WARN] Valkey is not configured. Redis backplane for MagicOnion will not work." -ForegroundColor Yellow
}

# Check Direct VPC Egress (required for internal communication)
$VpcEgressEnabled = $false
if ($env:VPC_NETWORK -and $env:VPC_SUBNET) {
    $VpcEgressEnabled = $true
}

$IMAGE = "$env:REGION-docker.pkg.dev/$env:PROJECT_ID/$env:REPO_NAME/game-realtime"

Write-Host ""
Write-Host "===== Deploy Configuration (Game.Realtime) =====" -ForegroundColor Cyan
Write-Host "PROJECT_ID:      $env:PROJECT_ID"
Write-Host "REGION:          $env:REGION"
Write-Host "SERVICE_NAME:    $env:SERVICE_NAME"
Write-Host "IMAGE:           ${IMAGE}:${Tag}"
if ($ValkeyEnabled) {
    Write-Host "VALKEY:          $env:VALKEY_HOST`:$env:VALKEY_PORT"
} else {
    Write-Host "VALKEY:          (not configured)"
}
if ($VpcEgressEnabled) {
    Write-Host "VPC_NETWORK:     $env:VPC_NETWORK"
    Write-Host "VPC_SUBNET:      $env:VPC_SUBNET"
}
Write-Host "MIN_INSTANCES:   1 (StreamingHub persistent connections)"
Write-Host "SESSION_AFFINITY: enabled"
Write-Host "HTTP/2:          enabled (gRPC)"
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Docker authentication
Write-Host "[1/4] Configuring Docker authentication..." -ForegroundColor Yellow
gcloud auth configure-docker "$env:REGION-docker.pkg.dev" --quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipBuild) {
    # Docker build
    Write-Host "[2/4] Building Docker image..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    docker build -t "${IMAGE}:${Tag}" -f docker/game-realtime/prod/Dockerfile .
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

    # Build environment variables
    $EnvVars = @(
        "ASPNETCORE_ENVIRONMENT=Production"
    )

    # Add JWT settings if configured
    if ($env:Jwt__Secret) { $EnvVars += "Jwt__Secret=$env:Jwt__Secret" }
    if ($env:Jwt__Issuer) { $EnvVars += "Jwt__Issuer=$env:Jwt__Issuer" }
    if ($env:Jwt__Audience) { $EnvVars += "Jwt__Audience=$env:Jwt__Audience" }

    # Add Unity Server settings if configured
    if ($env:UNITY_SERVER_ADDRESS) { $EnvVars += "UnityServer__ServerAddress=$env:UNITY_SERVER_ADDRESS" }
    if ($env:UNITY_SERVER_PORT) { $EnvVars += "UnityServer__ServerPort=$env:UNITY_SERVER_PORT" }

    # Add Valkey settings if configured
    if ($ValkeyEnabled) {
        $ValkeyPort = if ($env:VALKEY_PORT) { $env:VALKEY_PORT } else { "6379" }
        $EnvVars += "ConnectionStrings__Valkey=$env:VALKEY_HOST`:${ValkeyPort},abortConnect=false,connectTimeout=5000"
    }

    $EnvVarsString = $EnvVars -join ","

    # Build deploy command
    # Differences from Game.Server:
    #   No --add-cloudsql-instances (no DB)
    #   --min-instances=1          (persistent connections)
    #   --session-affinity         (StreamingHub sticky sessions)
    #   --use-http2                (gRPC required)
    #   --timeout=3600             (long-lived connections)
    #   --concurrency=100          (concurrent connections)
    $DeployArgs = @(
        "run", "deploy", $env:SERVICE_NAME,
        "--image=${IMAGE}:${Tag}",
        "--region=$env:REGION",
        "--platform=managed",
        "--allow-unauthenticated",
        "--set-env-vars=$EnvVarsString",
        "--memory=512Mi",
        "--cpu=1",
        "--min-instances=1",
        "--max-instances=10",
        "--concurrency=100",
        "--timeout=3600",
        "--session-affinity",
        "--use-http2"
    )

    # Add Direct VPC Egress (clear legacy VPC Connector)
    if ($VpcEgressEnabled) {
        $DeployArgs += "--clear-vpc-connector"
        $DeployArgs += "--network=$env:VPC_NETWORK"
        $DeployArgs += "--subnet=$env:VPC_SUBNET"
        $DeployArgs += "--vpc-egress=private-ranges-only"
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
