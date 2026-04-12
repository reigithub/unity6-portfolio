# docker/unity-server/prod/deploy.ps1
# Unity Dedicated Server GCE deployment script (PowerShell)
#
# Usage:
#   cd E:\UnityProjects\Unity6Portfolio\docker\unity-server\prod
#   .\deploy.ps1
#
# Cloud Run is NOT an option (UDP not supported)
# Deploys to GCE with Container-Optimized OS
#
# Options:
#   -BuildOnly      Build + push only (no deploy)
#   -SkipBuild      Skip build (deploy with existing image)
#   -Tag TAG         Image tag (default: latest)
#   -SetupInfra     First-time GCE infrastructure setup (firewall, health check)

param(
    [switch]$BuildOnly,
    [switch]$SkipBuild,
    [string]$Tag = "latest",
    [switch]$SetupInfra
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
$RequiredVars = @("PROJECT_ID", "REGION", "ZONE", "REPO_NAME", "INSTANCE_GROUP_NAME", "INSTANCE_TEMPLATE_NAME", "GAME_SERVER_URL", "SECRET_UNITY_SERVER_AUTH")
foreach ($var in $RequiredVars) {
    if (-not (Get-Item -Path "Env:$var" -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Required variable $var is not set in .env" -ForegroundColor Red
        exit 1
    }
}

# Default values
$MachineType = if ($env:MACHINE_TYPE) { $env:MACHINE_TYPE } else { "e2-medium" }
$UnityServerPort = if ($env:UNITY_SERVER_PORT) { $env:UNITY_SERVER_PORT } else { "7777" }
$UnityServerHealthPort = if ($env:UNITY_SERVER_HEALTH_PORT) { $env:UNITY_SERVER_HEALTH_PORT } else { "7778" }
$NetworkTag = if ($env:NETWORK_TAG) { $env:NETWORK_TAG } else { "unity-server" }
$HealthCheckName = if ($env:HEALTH_CHECK_NAME) { $env:HEALTH_CHECK_NAME } else { "unity-server-health-check" }

$IMAGE = "$env:REGION-docker.pkg.dev/$env:PROJECT_ID/$env:REPO_NAME/unity-server"
$BuildContext = "$ProjectRoot\src\Game.Client\Builds\Server\Linux"

Write-Host ""
Write-Host "===== Deploy Configuration (Unity Server -> GCE) =====" -ForegroundColor Cyan
Write-Host "PROJECT_ID:      $env:PROJECT_ID"
Write-Host "REGION:          $env:REGION"
Write-Host "ZONE:            $env:ZONE"
Write-Host "IMAGE:           ${IMAGE}:${Tag}"
Write-Host "MACHINE_TYPE:    $MachineType"
Write-Host "UNITY_SERVER_PORT:        $UnityServerPort (UDP)"
Write-Host "UNITY_SERVER_HEALTH_PORT: $UnityServerHealthPort (TCP)"
Write-Host "INSTANCE_GROUP:  $env:INSTANCE_GROUP_NAME"
Write-Host "NETWORK_TAG:     $NetworkTag"
Write-Host "BUILD_CONTEXT:   $BuildContext"
Write-Host "GAME_SERVER_URL: $env:GAME_SERVER_URL"
Write-Host "SECRET_NAME:     $env:SECRET_UNITY_SERVER_AUTH"
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# Infrastructure setup (first time only)
if ($SetupInfra) {
    Write-Host "[SETUP] Creating GCE infrastructure..." -ForegroundColor Yellow

    $Network = if ($env:NETWORK) { $env:NETWORK } else { "default" }
    $FwRuleGame = if ($env:FIREWALL_RULE_GAME) { $env:FIREWALL_RULE_GAME } else { "allow-unity-server-game" }
    $FwRuleHealth = if ($env:FIREWALL_RULE_HEALTH) { $env:FIREWALL_RULE_HEALTH } else { "allow-unity-server-health" }

    # Firewall rule: UDP (game traffic)
    Write-Host "[SETUP] Creating firewall rule for game traffic (UDP $UnityServerPort)..." -ForegroundColor Yellow
    gcloud compute firewall-rules create $FwRuleGame `
        --network=$Network `
        --allow="udp:$UnityServerPort" `
        --target-tags=$NetworkTag `
        --description="Allow UDP game traffic to Unity Server" `
        --quiet 2>$null
    # Ignore error if already exists

    # Firewall rule: TCP (health check)
    # GCE health check source IP ranges: 35.191.0.0/16, 130.211.0.0/22
    Write-Host "[SETUP] Creating firewall rule for health check (TCP $UnityServerHealthPort)..." -ForegroundColor Yellow
    gcloud compute firewall-rules create $FwRuleHealth `
        --network=$Network `
        --allow="tcp:$UnityServerHealthPort" `
        --source-ranges="35.191.0.0/16,130.211.0.0/22" `
        --target-tags=$NetworkTag `
        --description="Allow GCE health check to Unity Server" `
        --quiet 2>$null

    # TCP health check
    Write-Host "[SETUP] Creating TCP health check..." -ForegroundColor Yellow
    gcloud compute health-checks create tcp $HealthCheckName `
        --port=$UnityServerHealthPort `
        --check-interval=10s `
        --timeout=5s `
        --healthy-threshold=2 `
        --unhealthy-threshold=3 `
        --quiet 2>$null

    # Secret Manager IAM: Grant secretAccessor to GCE default service account
    Write-Host "[SETUP] Granting Secret Manager access to GCE default service account..." -ForegroundColor Yellow
    $DefaultSa = gcloud compute project-info describe --format='value(defaultServiceAccount)'
    gcloud secrets add-iam-policy-binding $env:SECRET_UNITY_SERVER_AUTH `
        --member="serviceAccount:$DefaultSa" `
        --role="roles/secretmanager.secretAccessor" `
        --project=$env:PROJECT_ID `
        --quiet 2>$null
    # Ignore error if binding already exists

    Write-Host "[SETUP] Infrastructure setup complete" -ForegroundColor Green
    Write-Host ""
}

# Docker authentication
Write-Host "[1/5] Configuring Docker authentication..." -ForegroundColor Yellow
gcloud auth configure-docker "$env:REGION-docker.pkg.dev" --quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipBuild) {
    # Check build context exists
    if (-not (Test-Path $BuildContext)) {
        Write-Host "[ERROR] Unity Server build output not found: $BuildContext" -ForegroundColor Red
        Write-Host "[ERROR] Build the Unity Dedicated Server first:" -ForegroundColor Red
        Write-Host "  Unity Editor -> Build > Server > Linux Dedicated Server" -ForegroundColor Yellow
        exit 1
    }

    # Docker build
    Write-Host "[2/5] Building Docker image..." -ForegroundColor Yellow
    docker build -t "${IMAGE}:${Tag}" `
        -f "$ProjectRoot\docker\unity-server\Dockerfile" `
        "$BuildContext"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Push to registry
    Write-Host "[3/5] Pushing to Artifact Registry..." -ForegroundColor Yellow
    docker push "${IMAGE}:${Tag}"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "[2/5] Skipping build..." -ForegroundColor Gray
    Write-Host "[3/5] Skipping push..." -ForegroundColor Gray
}

if (-not $BuildOnly) {
    # Fetch HMAC secret from Secret Manager
    Write-Host "[INFO] Fetching secret from Secret Manager ($env:SECRET_UNITY_SERVER_AUTH)..." -ForegroundColor Yellow
    $UnitySecret = gcloud secrets versions access latest `
        --secret="$env:SECRET_UNITY_SERVER_AUTH" `
        --project="$env:PROJECT_ID"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrEmpty($UnitySecret)) {
        Write-Host "[ERROR] Secret Manager から UNITY_SERVER_AUTH_SESSION_SECRET を取得できませんでした" -ForegroundColor Red
        exit 1
    }

    # Create instance template
    Write-Host "[4/5] Creating instance template..." -ForegroundColor Yellow
    $TemplateName = "$env:INSTANCE_TEMPLATE_NAME-$Tag"

    gcloud compute instance-templates create-with-container $TemplateName `
        --machine-type=$MachineType `
        --tags=$NetworkTag `
        --container-image="${IMAGE}:${Tag}" `
        --container-env="UNITY_SERVER_AUTH_SESSION_SECRET=$UnitySecret" `
        --container-env="GAME_SERVER_URL=$env:GAME_SERVER_URL" `
        --container-env="UNITY_SERVER_PORT=$UnityServerPort" `
        --container-env="UNITY_SERVER_HEALTH_PORT=$UnityServerHealthPort" `
        --container-arg="--port" `
        --container-arg=$UnityServerPort `
        --container-arg="--health-port" `
        --container-arg=$UnityServerHealthPort `
        --scopes=https://www.googleapis.com/auth/cloud-platform `
        --region=$env:REGION `
        --quiet 2>$null

    # Update or create MIG
    Write-Host "[5/5] Updating Managed Instance Group..." -ForegroundColor Yellow
    $MigExists = gcloud compute instance-groups managed describe $env:INSTANCE_GROUP_NAME `
        --zone=$env:ZONE 2>$null
    if ($MigExists) {
        # Update existing MIG
        gcloud compute instance-groups managed set-instance-template `
            $env:INSTANCE_GROUP_NAME `
            --zone=$env:ZONE `
            --template=$TemplateName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        gcloud compute instance-groups managed rolling-action start-update `
            $env:INSTANCE_GROUP_NAME `
            --zone=$env:ZONE `
            --version="template=$TemplateName" `
            --max-surge=1 `
            --max-unavailable=0
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        # Create new MIG
        gcloud compute instance-groups managed create $env:INSTANCE_GROUP_NAME `
            --zone=$env:ZONE `
            --template=$TemplateName `
            --size=1 `
            --health-check=$HealthCheckName `
            --initial-delay=60
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host ""
    Write-Host "===== Deploy Complete =====" -ForegroundColor Green
    Write-Host "Instance Group: $env:INSTANCE_GROUP_NAME" -ForegroundColor Cyan
    Write-Host "Template:       $TemplateName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Check status:" -ForegroundColor Yellow
    Write-Host "  gcloud compute instance-groups managed list-instances $env:INSTANCE_GROUP_NAME --zone=$env:ZONE"
} else {
    Write-Host "[4/5] Skipping template creation (BuildOnly mode)..." -ForegroundColor Gray
    Write-Host "[5/5] Skipping deploy (BuildOnly mode)..." -ForegroundColor Gray
    Write-Host ""
    Write-Host "===== Build Complete =====" -ForegroundColor Green
    Write-Host "Image: ${IMAGE}:${Tag}" -ForegroundColor Cyan
}
