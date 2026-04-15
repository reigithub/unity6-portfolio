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
#   -BuildOnly                  Build + push only (no deploy)
#   -SkipBuild                  Skip build (deploy with existing image)
#   -ImageTag TAG               Artifact Registry image tag (default: latest)
#   -TemplateSuffix SUFFIX      Instance-template name suffix
#                               If empty, "{ImageTag}-{UnixTime}" is auto-generated
#                               so re-runs do not collide on alreadyExists.
#   -InitialDelay SECONDS       Autohealing initial-delay (default: 180)
#                               Allows time for first docker pull + Unity DS startup.
#   -Force                      Override rolling-action-in-progress check
#                               (will disconnect active sessions)
#   -SetupInfra                 First-time GCE infrastructure setup (firewall, health check)
#   -Tag TAG                    DEPRECATED: equivalent to -ImageTag and -TemplateSuffix
#                               Will be removed in next major version.
#
# Environment file (.env) variable INITIAL_DELAY overrides default 180 if set.

param(
    [switch]$BuildOnly,
    [switch]$SkipBuild,
    [string]$ImageTag = "latest",
    [string]$TemplateSuffix = "",
    [int]$InitialDelay = 180,
    [switch]$Force,
    [switch]$SetupInfra,
    [string]$Tag = ""
)

# gcloud は warning を stderr に出して exit 0 で返すため、Stop だと致命扱いされて誤中断する。
# Continue にして $LASTEXITCODE を個別判定する方針。
$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\..\..\..\"

# Backward-compat: -Tag を ImageTag/TemplateSuffix の両方に適用
if ($Tag) {
    if (-not $PSBoundParameters.ContainsKey('ImageTag')) { $ImageTag = $Tag }
    if (-not $PSBoundParameters.ContainsKey('TemplateSuffix')) { $TemplateSuffix = $Tag }
    Write-Host "[DEPRECATED] -Tag is deprecated, will be removed in next major version. Use -ImageTag and -TemplateSuffix." -ForegroundColor Yellow
}

# TemplateSuffix が空なら UnixTime を自動付与 → 同 ImageTag での再実行衝突回避 + ロールバック互換性
if (-not $TemplateSuffix) {
    $TemplateSuffix = "$ImageTag-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
}

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

# .env で INITIAL_DELAY が指定されていれば上書き（ただし -InitialDelay の明示指定が優先）
if ($env:INITIAL_DELAY -and -not $PSBoundParameters.ContainsKey('InitialDelay')) {
    $InitialDelay = [int]$env:INITIAL_DELAY
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
$TemplateName = "$env:INSTANCE_TEMPLATE_NAME-$TemplateSuffix"

Write-Host ""
Write-Host "===== Deploy Configuration (Unity Server -> GCE) =====" -ForegroundColor Cyan
Write-Host "PROJECT_ID:      $env:PROJECT_ID"
Write-Host "REGION:          $env:REGION"
Write-Host "ZONE:            $env:ZONE"
Write-Host "IMAGE:           ${IMAGE}:${ImageTag}"
Write-Host "TEMPLATE_NAME:   $TemplateName"
Write-Host "INITIAL_DELAY:   $InitialDelay seconds"
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

# 失敗時に gcloud の stderr を保全して表示するヘルパ。
# Description: ログ表示用の操作名。Command: gcloud 呼出を含む scriptblock。
function Invoke-Gcloud {
    param([string]$Description, [scriptblock]$Command)
    $captured = & $Command 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] $Description failed (exit=$LASTEXITCODE)" -ForegroundColor Red
        Write-Host $captured -ForegroundColor DarkGray
        exit $LASTEXITCODE
    }
    return $captured
}

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
        --quiet 2>&1 | Out-Null
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
        --quiet 2>&1 | Out-Null

    # Firewall rule: TCP (Cloud Run Direct VPC Egress → DS internal communication)
    # Game.Server が Direct VPC Egress 経由で DS の /session/start に HTTP POST を送信するために必要
    $FwRuleInternal = if ($env:FIREWALL_RULE_INTERNAL) { $env:FIREWALL_RULE_INTERNAL } else { "allow-unity-server-internal" }
    $VpcConnectorSubnet = if ($env:VPC_CONNECTOR_SUBNET) { $env:VPC_CONNECTOR_SUBNET } else { "10.10.0.0/26" }
    Write-Host "[SETUP] Creating firewall rule for internal traffic (TCP $UnityServerHealthPort from Direct VPC Egress $VpcConnectorSubnet)..." -ForegroundColor Yellow
    gcloud compute firewall-rules create $FwRuleInternal `
        --network=$Network `
        --allow="tcp:$UnityServerHealthPort" `
        --source-ranges="$VpcConnectorSubnet" `
        --target-tags=$NetworkTag `
        --description="Allow Cloud Run Direct VPC Egress to send session/start to Unity Server" `
        --quiet 2>&1 | Out-Null

    # TCP health check
    Write-Host "[SETUP] Creating TCP health check..." -ForegroundColor Yellow
    gcloud compute health-checks create tcp $HealthCheckName `
        --port=$UnityServerHealthPort `
        --check-interval=10s `
        --timeout=5s `
        --healthy-threshold=2 `
        --unhealthy-threshold=3 `
        --quiet 2>&1 | Out-Null

    # Secret Manager IAM: Grant secretAccessor to GCE default service account
    Write-Host "[SETUP] Granting Secret Manager access to GCE default service account..." -ForegroundColor Yellow
    $DefaultSa = gcloud compute project-info describe --format='value(defaultServiceAccount)'
    gcloud secrets add-iam-policy-binding $env:SECRET_UNITY_SERVER_AUTH `
        --member="serviceAccount:$DefaultSa" `
        --role="roles/secretmanager.secretAccessor" `
        --project=$env:PROJECT_ID `
        --quiet 2>&1 | Out-Null

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
    docker build -t "${IMAGE}:${ImageTag}" `
        -f "$ProjectRoot\docker\unity-server\Dockerfile" `
        "$BuildContext"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Push to registry
    Write-Host "[3/5] Pushing to Artifact Registry..." -ForegroundColor Yellow
    docker push "${IMAGE}:${ImageTag}"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "[2/5] Skipping build..." -ForegroundColor Gray
    Write-Host "[3/5] Skipping push..." -ForegroundColor Gray
}

if (-not $BuildOnly) {
    # Verify image manifest exists in registry before proceeding (fail-fast)
    # Requires CI service account to have roles/artifactregistry.reader
    Write-Host "[CHECK] Verifying image exists in Artifact Registry..." -ForegroundColor Yellow
    $null = gcloud artifacts docker images describe "${IMAGE}:${ImageTag}" `
        --format="value(image_summary.digest)" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Image not found: ${IMAGE}:${ImageTag}" -ForegroundColor Red
        Write-Host "[HINT] Run without -SkipBuild to build & push first," -ForegroundColor Yellow
        Write-Host "       or specify an existing -ImageTag." -ForegroundColor Yellow
        Write-Host "[HINT] CI service account requires roles/artifactregistry.reader." -ForegroundColor Yellow
        exit 1
    }

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
    Write-Host "[4/5] Creating instance template $TemplateName..." -ForegroundColor Yellow

    Invoke-Gcloud "Create instance template" {
        gcloud compute instance-templates create-with-container $TemplateName `
            --machine-type=$MachineType `
            --tags=$NetworkTag `
            --container-image="${IMAGE}:${ImageTag}" `
            --container-env="UNITY_SERVER_AUTH_SESSION_SECRET=$UnitySecret" `
            --container-env="GAME_SERVER_URL=$env:GAME_SERVER_URL" `
            --container-env="UNITY_SERVER_PORT=$UnityServerPort" `
            --container-env="UNITY_SERVER_HEALTH_PORT=$UnityServerHealthPort" `
            --container-arg="--port" `
            --container-arg=$UnityServerPort `
            --container-arg="--health-port" `
            --container-arg=$UnityServerHealthPort `
            --scopes=https://www.googleapis.com/auth/cloud-platform `
            --region=$env:REGION
    } | Out-Null

    # Update or create MIG
    Write-Host "[5/5] Updating Managed Instance Group..." -ForegroundColor Yellow

    # MIG 存在判定（失敗が想定内なので 2>$null は維持）
    $MigExists = gcloud compute instance-groups managed describe $env:INSTANCE_GROUP_NAME `
        --zone=$env:ZONE 2>$null

    if ($MigExists) {
        # 進行中の rolling-action がある場合は -Force なしで中止（接続中ユーザー保護）
        $isReached = gcloud compute instance-groups managed describe `
            $env:INSTANCE_GROUP_NAME --zone=$env:ZONE `
            --format="value(status.versionTarget.isReached)" 2>$null
        if ($isReached -eq "False" -and -not $Force) {
            Write-Host "[ERROR] Rolling update is already in progress." -ForegroundColor Red
            Write-Host "[HINT] Wait for completion, or use -Force to override (will disconnect active sessions)." -ForegroundColor Yellow
            exit 1
        }

        Invoke-Gcloud "Set instance template" {
            gcloud compute instance-groups managed set-instance-template `
                $env:INSTANCE_GROUP_NAME `
                --zone=$env:ZONE `
                --template=$TemplateName
        } | Out-Null

        # autohealing initial-delay の更新は致命でない（template 切替成功すれば deploy 全体は成功扱い）
        gcloud compute instance-groups managed update `
            $env:INSTANCE_GROUP_NAME `
            --zone=$env:ZONE `
            --initial-delay=$InitialDelay 2>&1 | Out-Null

        # rolling-action: --max-unavailable=0 と RESTART の衝突を回避するため --minimal-action=replace を明示
        Invoke-Gcloud "Start rolling update" {
            gcloud compute instance-groups managed rolling-action start-update `
                $env:INSTANCE_GROUP_NAME `
                --zone=$env:ZONE `
                --version="template=$TemplateName" `
                --max-surge=1 `
                --max-unavailable=0 `
                --minimal-action=replace
        } | Out-Null
    } else {
        # Create new MIG
        Invoke-Gcloud "Create new MIG" {
            gcloud compute instance-groups managed create $env:INSTANCE_GROUP_NAME `
                --zone=$env:ZONE `
                --template=$TemplateName `
                --size=1 `
                --health-check=$HealthCheckName `
                --initial-delay=$InitialDelay
        } | Out-Null
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
    Write-Host "Image: ${IMAGE}:${ImageTag}" -ForegroundColor Cyan
}
