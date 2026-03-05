#!/bin/bash
# docker/unity-server/prod/deploy.sh
# Unity Dedicated Server GCE デプロイスクリプト（bash）
#
# 使用方法:
#   cd Unity6Portfolio/docker/unity-server/prod
#   chmod +x deploy.sh
#   ./deploy.sh
#
# Cloud Run ではなく GCE (Container-Optimized OS) にデプロイ
# Unity Server は UDP を使用するため Cloud Run は使用不可
#
# オプション:
#   --build-only    ビルド＋プッシュのみ（デプロイしない）
#   --skip-build    ビルドをスキップ（既存イメージでデプロイ）
#   --tag TAG       イメージタグ指定（デフォルト: latest）
#   --setup-infra   GCE インフラ初期セットアップ（ファイアウォール、ヘルスチェック）

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

BUILD_ONLY=false
SKIP_BUILD=false
TAG="latest"
SETUP_INFRA=false

# 引数解析
while [[ $# -gt 0 ]]; do
    case $1 in
        --build-only) BUILD_ONLY=true; shift ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        --tag) TAG="$2"; shift 2 ;;
        --setup-infra) SETUP_INFRA=true; shift ;;
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
REQUIRED_VARS=("PROJECT_ID" "REGION" "ZONE" "REPO_NAME" "INSTANCE_GROUP_NAME" "INSTANCE_TEMPLATE_NAME")
for var in "${REQUIRED_VARS[@]}"; do
    if [[ -z "${!var}" ]]; then
        echo "[ERROR] Required variable $var is not set in .env"
        exit 1
    fi
done

# デフォルト値
MACHINE_TYPE="${MACHINE_TYPE:-e2-medium}"
GAME_PORT="${GAME_PORT:-7777}"
HEALTH_PORT="${HEALTH_PORT:-7778}"
MAX_PLAYERS="${MAX_PLAYERS:-4}"
NETWORK_TAG="${NETWORK_TAG:-unity-server}"
HEALTH_CHECK_NAME="${HEALTH_CHECK_NAME:-unity-server-health-check}"

IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}/unity-server"

# Unity Server ビルド出力パス
BUILD_CONTEXT="${PROJECT_ROOT}/src/Game.Client/Builds/Server/Linux"

echo ""
echo "===== Deploy Configuration (Unity Server -> GCE) ====="
echo "PROJECT_ID:      $PROJECT_ID"
echo "REGION:          $REGION"
echo "ZONE:            $ZONE"
echo "IMAGE:           ${IMAGE}:${TAG}"
echo "MACHINE_TYPE:    $MACHINE_TYPE"
echo "GAME_PORT:       $GAME_PORT (UDP)"
echo "HEALTH_PORT:     $HEALTH_PORT (TCP)"
echo "MAX_PLAYERS:     $MAX_PLAYERS"
echo "INSTANCE_GROUP:  $INSTANCE_GROUP_NAME"
echo "NETWORK_TAG:     $NETWORK_TAG"
echo "BUILD_CONTEXT:   $BUILD_CONTEXT"
echo "======================================================="
echo ""

# インフラセットアップ（初回のみ）
if [[ "$SETUP_INFRA" == "true" ]]; then
    echo "[SETUP] Creating GCE infrastructure..."

    # ファイアウォールルール: UDP (ゲームトラフィック)
    echo "[SETUP] Creating firewall rule for game traffic (UDP ${GAME_PORT})..."
    gcloud compute firewall-rules create "${FIREWALL_RULE_GAME:-allow-unity-server-game}" \
        --network="${NETWORK:-default}" \
        --allow="udp:${GAME_PORT}" \
        --target-tags="${NETWORK_TAG}" \
        --description="Allow UDP game traffic to Unity Server" \
        --quiet 2>/dev/null || echo "  (firewall rule already exists)"

    # ファイアウォールルール: TCP (ヘルスチェック)
    # GCE ヘルスチェックのソース IP レンジ: 35.191.0.0/16, 130.211.0.0/22
    echo "[SETUP] Creating firewall rule for health check (TCP ${HEALTH_PORT})..."
    gcloud compute firewall-rules create "${FIREWALL_RULE_HEALTH:-allow-unity-server-health}" \
        --network="${NETWORK:-default}" \
        --allow="tcp:${HEALTH_PORT}" \
        --source-ranges="35.191.0.0/16,130.211.0.0/22" \
        --target-tags="${NETWORK_TAG}" \
        --description="Allow GCE health check to Unity Server" \
        --quiet 2>/dev/null || echo "  (firewall rule already exists)"

    # TCP ヘルスチェック作成
    echo "[SETUP] Creating TCP health check..."
    gcloud compute health-checks create tcp "${HEALTH_CHECK_NAME}" \
        --port="${HEALTH_PORT}" \
        --check-interval=10s \
        --timeout=5s \
        --healthy-threshold=2 \
        --unhealthy-threshold=3 \
        --quiet 2>/dev/null || echo "  (health check already exists)"

    echo "[SETUP] Infrastructure setup complete"
    echo ""
fi

# Docker 認証
echo "[1/5] Configuring Docker authentication..."
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

if [[ "$SKIP_BUILD" != "true" ]]; then
    # ビルドコンテキストの確認
    if [[ ! -d "$BUILD_CONTEXT" ]]; then
        echo "[ERROR] Unity Server build output not found: $BUILD_CONTEXT"
        echo "[ERROR] Build the Unity Dedicated Server first:"
        echo "  Unity Editor -> Build > Server > Linux Dedicated Server"
        exit 1
    fi

    # Docker ビルド
    echo "[2/5] Building Docker image..."
    docker build -t "${IMAGE}:${TAG}" \
        -f "${PROJECT_ROOT}/docker/unity-server/Dockerfile" \
        "$BUILD_CONTEXT"

    # プッシュ
    echo "[3/5] Pushing to Artifact Registry..."
    docker push "${IMAGE}:${TAG}"
else
    echo "[2/5] Skipping build..."
    echo "[3/5] Skipping push..."
fi

if [[ "$BUILD_ONLY" != "true" ]]; then
    # インスタンステンプレート作成
    echo "[4/5] Creating instance template..."
    TEMPLATE_NAME="${INSTANCE_TEMPLATE_NAME}-${TAG}"

    gcloud compute instance-templates create-with-container "${TEMPLATE_NAME}" \
        --machine-type="${MACHINE_TYPE}" \
        --tags="${NETWORK_TAG}" \
        --container-image="${IMAGE}:${TAG}" \
        --container-arg="--port" \
        --container-arg="${GAME_PORT}" \
        --container-arg="--health-port" \
        --container-arg="${HEALTH_PORT}" \
        --container-arg="--players" \
        --container-arg="${MAX_PLAYERS}" \
        --scopes=https://www.googleapis.com/auth/cloud-platform \
        --region="${REGION}" \
        --quiet 2>/dev/null \
        || echo "  (template may already exist, continuing...)"

    # MIG 更新 or 作成
    echo "[5/5] Updating Managed Instance Group..."
    if gcloud compute instance-groups managed describe "${INSTANCE_GROUP_NAME}" \
        --zone="${ZONE}" 2>/dev/null; then
        # 既存 MIG を更新
        gcloud compute instance-groups managed set-instance-template \
            "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --template="${TEMPLATE_NAME}"

        gcloud compute instance-groups managed rolling-action start-update \
            "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --version="template=${TEMPLATE_NAME}" \
            --max-surge=1 \
            --max-unavailable=0
    else
        # 新規 MIG を作成
        gcloud compute instance-groups managed create "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --template="${TEMPLATE_NAME}" \
            --size=1 \
            --health-check="${HEALTH_CHECK_NAME}" \
            --initial-delay=60
    fi

    echo ""
    echo "===== Deploy Complete ====="
    echo "Instance Group: ${INSTANCE_GROUP_NAME}"
    echo "Template:       ${TEMPLATE_NAME}"
    echo ""
    echo "Check status:"
    echo "  gcloud compute instance-groups managed list-instances ${INSTANCE_GROUP_NAME} --zone=${ZONE}"
else
    echo "[4/5] Skipping template creation (BuildOnly mode)..."
    echo "[5/5] Skipping deploy (BuildOnly mode)..."
    echo ""
    echo "===== Build Complete ====="
    echo "Image: ${IMAGE}:${TAG}"
fi
