#!/bin/bash
# docker/game-realtime/prod/deploy.sh
# Game.Realtime Cloud Run デプロイスクリプト（bash）
#
# 使用方法:
#   cd Unity6Portfolio/docker/game-realtime/prod
#   chmod +x deploy.sh
#   ./deploy.sh
#
# Cloud Run 設定（Game.Server との違い）:
#   - Cloud SQL 不要（Valkey のみ使用）
#   - min-instances=1（StreamingHub 常時接続のため）
#   - session-affinity 有効（StreamingHub スティッキーセッション）
#   - HTTP/2 有効（gRPC 通信）

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

# 必須変数の確認（Cloud SQL は不要）
REQUIRED_VARS=("PROJECT_ID" "REGION" "REPO_NAME" "SERVICE_NAME")
for var in "${REQUIRED_VARS[@]}"; do
    if [[ -z "${!var}" ]]; then
        echo "[ERROR] Required variable $var is not set in .env"
        exit 1
    fi
done

# JWT 設定の確認（警告のみ）
if [[ -z "$Jwt__Secret" ]]; then
    echo "[WARN] Jwt__Secret is not set. JWT authentication may fail."
fi

# Valkey 設定の確認（Game.Realtime では必須レベル）
VALKEY_ENABLED=false
if [[ -n "$VALKEY_HOST" && -n "$VPC_NETWORK" && -n "$VPC_SUBNET" ]]; then
    VALKEY_ENABLED=true
elif [[ -n "$VALKEY_HOST" ]]; then
    echo "[WARN] Valkey requires VALKEY_HOST, VPC_NETWORK, and VPC_SUBNET to be set."
fi
if [[ "$VALKEY_ENABLED" != "true" ]]; then
    echo "[WARN] Valkey is not configured. Redis backplane for MagicOnion will not work."
fi

# Direct VPC Egress の確認（内部通信に必要）
VPC_EGRESS_ENABLED=false
if [[ -n "$VPC_NETWORK" && -n "$VPC_SUBNET" ]]; then
    VPC_EGRESS_ENABLED=true
fi

IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}/game-realtime"

echo ""
echo "===== Deploy Configuration (Game.Realtime) ====="
echo "PROJECT_ID:      $PROJECT_ID"
echo "REGION:          $REGION"
echo "SERVICE_NAME:    $SERVICE_NAME"
echo "IMAGE:           ${IMAGE}:${TAG}"
if [[ "$VALKEY_ENABLED" == "true" ]]; then
    echo "VALKEY:          ${VALKEY_HOST}:${VALKEY_PORT:-6379}"
else
    echo "VALKEY:          (not configured)"
fi
if [[ "$VPC_EGRESS_ENABLED" == "true" ]]; then
    echo "VPC_NETWORK:     $VPC_NETWORK"
    echo "VPC_SUBNET:      $VPC_SUBNET"
fi
echo "MIN_INSTANCES:   1 (StreamingHub persistent connections)"
echo "SESSION_AFFINITY: enabled"
echo "HTTP/2:          enabled (gRPC)"
echo "================================================="
echo ""

# Docker 認証
echo "[1/4] Configuring Docker authentication..."
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

if [[ "$SKIP_BUILD" != "true" ]]; then
    # Docker ビルド
    echo "[2/4] Building Docker image..."
    cd "$PROJECT_ROOT"
    docker build -t "${IMAGE}:${TAG}" -f docker/game-realtime/prod/Dockerfile .

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

    # 環境変数を構築
    ENV_VARS="ASPNETCORE_ENVIRONMENT=Production"

    # JWT 設定を追加
    [[ -n "$Jwt__Secret" ]] && ENV_VARS="$ENV_VARS,Jwt__Secret=$Jwt__Secret"
    [[ -n "$Jwt__Issuer" ]] && ENV_VARS="$ENV_VARS,Jwt__Issuer=$Jwt__Issuer"
    [[ -n "$Jwt__Audience" ]] && ENV_VARS="$ENV_VARS,Jwt__Audience=$Jwt__Audience"

    # Unity Server 接続設定
    [[ -n "$UNITY_SERVER_ADDRESS" ]] && ENV_VARS="$ENV_VARS,UnityServer__ServerAddress=$UNITY_SERVER_ADDRESS"
    [[ -n "$UNITY_SERVER_PORT" ]] && ENV_VARS="$ENV_VARS,UnityServer__ServerPort=$UNITY_SERVER_PORT"

    # Valkey 設定を追加
    if [[ "$VALKEY_ENABLED" == "true" ]]; then
        VALKEY_PORT="${VALKEY_PORT:-6379}"
        ENV_VARS="$ENV_VARS,ConnectionStrings__Valkey=${VALKEY_HOST}:${VALKEY_PORT},abortConnect=false,connectTimeout=5000"
    fi

    # デプロイコマンドを構築
    # Game.Server との違い:
    #   --no-cloudsql-instances   (DB 不要)
    #   --min-instances=1         (常時接続維持)
    #   --session-affinity        (StreamingHub スティッキーセッション)
    #   --use-http2               (gRPC 必須)
    #   --timeout=3600            (長時間接続対応)
    #   --concurrency=100         (同時接続数)
    DEPLOY_ARGS=(
        "run" "deploy" "$SERVICE_NAME"
        "--image=${IMAGE}:${TAG}"
        "--region=$REGION"
        "--platform=managed"
        "--allow-unauthenticated"
        "--set-env-vars=$ENV_VARS"
        "--memory=512Mi"
        "--cpu=1"
        "--min-instances=1"
        "--max-instances=10"
        "--concurrency=100"
        "--timeout=3600"
        "--session-affinity"
        "--use-http2"
    )

    # Direct VPC Egress を追加（レガシー VPC Connector をクリア）
    if [[ "$VPC_EGRESS_ENABLED" == "true" ]]; then
        DEPLOY_ARGS+=("--clear-vpc-connector")
        DEPLOY_ARGS+=("--network=$VPC_NETWORK")
        DEPLOY_ARGS+=("--subnet=$VPC_SUBNET")
        DEPLOY_ARGS+=("--vpc-egress=private-ranges-only")
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
