#!/bin/bash
# docker/game-server/prod/deploy.sh
# Cloud Run デプロイスクリプト（bash）
#
# 使用方法:
#   cd Unity6Portfolio/docker/game-server/prod
#   chmod +x deploy.sh
#   ./deploy.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

BUILD_ONLY=false
SKIP_BUILD=false
TAG="latest"

# 引数解析
while [[ $# -gt 0 ]]; do
    case $1 in
        --build-only) BUILD_ONLY=true; shift ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        --tag) TAG="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# .env ファイルを読み込み
ENV_FILE="$SCRIPT_DIR/.env"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    source <(grep -v '^#' "$ENV_FILE" | grep -v '^\s*$' | sed 's/\r$//')
    set +a
    echo "[OK] .env loaded"
else
    echo "[ERROR] .env file not found. Copy .env.example to .env and configure."
    exit 1
fi

# 必須変数の確認
REQUIRED_VARS=("PROJECT_ID" "REGION" "REPO_NAME" "SERVICE_NAME" "INSTANCE_NAME" "SECRET_DB_CONNECTION" "SECRET_JWT" "SECRET_REQUEST_SIGNING")
for var in "${REQUIRED_VARS[@]}"; do
    if [[ -z "${!var}" ]]; then
        echo "[ERROR] Required variable $var is not set in .env"
        exit 1
    fi
done

IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}/game-server"

# Cloud SQL 接続名を取得
echo "[0/4] Getting Cloud SQL connection name..."
CONNECTION_NAME=$(gcloud sql instances describe "$INSTANCE_NAME" --format="value(connectionName)" 2>/dev/null)
if [[ -z "$CONNECTION_NAME" ]]; then
    echo "[ERROR] Failed to get Cloud SQL connection name for instance: $INSTANCE_NAME"
    exit 1
fi

# Valkey 設定の確認
VALKEY_ENABLED=false
if [[ -n "$VALKEY_HOST" && -n "$VPC_CONNECTOR" ]]; then
    VALKEY_ENABLED=true
elif [[ -n "$VALKEY_HOST" || -n "$VPC_CONNECTOR" ]]; then
    echo "[WARN] Valkey requires both VALKEY_HOST and VPC_CONNECTOR to be set."
fi

echo ""
echo "===== Deploy Configuration ====="
echo "PROJECT_ID:      $PROJECT_ID"
echo "REGION:          $REGION"
echo "SERVICE_NAME:    $SERVICE_NAME"
echo "IMAGE:           ${IMAGE}:${TAG}"
echo "CLOUD_SQL:       $CONNECTION_NAME"
echo "DATABASE:        $DB_NAME"
if [[ "$VALKEY_ENABLED" == "true" ]]; then
    echo "VALKEY:          ${VALKEY_HOST}:${VALKEY_PORT:-6379}"
    echo "VPC_CONNECTOR:   $VPC_CONNECTOR"
else
    echo "VALKEY:          (not configured)"
fi
echo "================================="
echo ""

# Docker 認証
echo "[1/4] Configuring Docker authentication..."
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

if [[ "$SKIP_BUILD" != "true" ]]; then
    # Docker ビルド
    echo "[2/4] Building Docker image..."
    cd "$PROJECT_ROOT"
    docker build -t "${IMAGE}:${TAG}" -f docker/game-server/prod/Dockerfile .

    # プッシュ
    echo "[3/4] Pushing to Artifact Registry..."
    docker push "${IMAGE}:${TAG}"
else
    echo "[2/4] Skipping build..."
    echo "[3/4] Skipping push..."
fi

if [[ "$BUILD_ONLY" != "true" ]]; then
    # Cloud Run デプロイ
    echo "[4/4] Deploying to Cloud Run..."

    # 環境変数を構築（非機密のみ）
    ENV_VARS="ASPNETCORE_ENVIRONMENT=Production"
    [[ -n "$Jwt__Issuer" ]] && ENV_VARS="$ENV_VARS,Jwt__Issuer=$Jwt__Issuer"
    [[ -n "$Jwt__Audience" ]] && ENV_VARS="$ENV_VARS,Jwt__Audience=$Jwt__Audience"

    # Valkey 設定を追加（設定されている場合）
    if [[ "$VALKEY_ENABLED" == "true" ]]; then
        VALKEY_PORT="${VALKEY_PORT:-6379}"
        ENV_VARS="$ENV_VARS,ConnectionStrings__Valkey=${VALKEY_HOST}:${VALKEY_PORT},abortConnect=false,connectTimeout=5000"
    fi

    # Secret Manager シークレットを構築
    SECRETS="ConnectionStrings__Default=${SECRET_DB_CONNECTION}:latest"
    SECRETS="$SECRETS,Jwt__Secret=${SECRET_JWT}:latest"
    SECRETS="$SECRETS,RequestSigning__SecretKey=${SECRET_REQUEST_SIGNING}:latest"
    [[ -n "$SECRET_RESEND" ]] && SECRETS="$SECRETS,Resend__ApiKey=${SECRET_RESEND}:latest"

    # デプロイコマンドを構築
    DEPLOY_ARGS=(
        "run" "deploy" "$SERVICE_NAME"
        "--image=${IMAGE}:${TAG}"
        "--region=$REGION"
        "--platform=managed"
        "--allow-unauthenticated"
        "--add-cloudsql-instances=$CONNECTION_NAME"
        "--set-env-vars=$ENV_VARS"
        "--set-secrets=$SECRETS"
        "--memory=512Mi"
        "--cpu=1"
        "--min-instances=0"
        "--max-instances=10"
        "--concurrency=80"
        "--timeout=300"
    )

    # VPC Connector を追加（Valkey 有効時）
    if [[ "$VALKEY_ENABLED" == "true" ]]; then
        DEPLOY_ARGS+=("--vpc-connector=$VPC_CONNECTOR")
    fi

    gcloud "${DEPLOY_ARGS[@]}"

    # URL 表示
    echo ""
    echo "===== Deploy Complete ====="
    URL=$(gcloud run services describe "$SERVICE_NAME" --region="$REGION" --format="value(status.url)")
    echo "Service URL: $URL"
else
    echo "[4/4] Skipping deploy (BuildOnly mode)..."
    echo ""
    echo "===== Build Complete ====="
    echo "Image: ${IMAGE}:${TAG}"
fi
