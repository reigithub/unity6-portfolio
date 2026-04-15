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
#   --build-only              ビルド＋プッシュのみ（デプロイしない）
#   --skip-build              ビルドをスキップ（既存イメージでデプロイ）
#   --image-tag TAG           Artifact Registry の image tag（デフォルト: latest）
#   --template-suffix SUFFIX  instance-template 名サフィックス
#                             空なら "{ImageTag}-{UnixTime}" を自動付与し、
#                             再実行で alreadyExists 衝突を回避する。
#   --initial-delay SECONDS   autohealing initial-delay（デフォルト: 180 秒）
#                             初回 docker pull + Unity DS 起動を待つための猶予。
#   --force                   rolling-action 進行中でも実行（接続中ユーザーは切断される）
#   --setup-infra             GCE インフラ初期セットアップ（ファイアウォール、ヘルスチェック）
#   --tag TAG                 DEPRECATED: --image-tag と --template-suffix の両方に適用される
#                             次期メジャー版で削除予定。
#
# .env の INITIAL_DELAY が設定されている場合、デフォルト 180 を上書きする。

# gcloud は warning を stderr に出して exit 0 で返すため、set -e ではなく
# 各コマンドの $? を個別判定する方針。
set +e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

BUILD_ONLY=false
SKIP_BUILD=false
IMAGE_TAG="latest"
TEMPLATE_SUFFIX=""
INITIAL_DELAY=180
FORCE=false
SETUP_INFRA=false
LEGACY_TAG=""
IMAGE_TAG_EXPLICIT=false
TEMPLATE_SUFFIX_EXPLICIT=false
INITIAL_DELAY_EXPLICIT=false

# 引数解析
while [[ $# -gt 0 ]]; do
    case $1 in
        --build-only) BUILD_ONLY=true; shift ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        --image-tag) IMAGE_TAG="$2"; IMAGE_TAG_EXPLICIT=true; shift 2 ;;
        --template-suffix) TEMPLATE_SUFFIX="$2"; TEMPLATE_SUFFIX_EXPLICIT=true; shift 2 ;;
        --initial-delay) INITIAL_DELAY="$2"; INITIAL_DELAY_EXPLICIT=true; shift 2 ;;
        --force) FORCE=true; shift ;;
        --setup-infra) SETUP_INFRA=true; shift ;;
        --tag) LEGACY_TAG="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Backward-compat: --tag を image-tag/template-suffix の両方に適用
if [[ -n "$LEGACY_TAG" ]]; then
    if [[ "$IMAGE_TAG_EXPLICIT" == "false" ]]; then IMAGE_TAG="$LEGACY_TAG"; fi
    if [[ "$TEMPLATE_SUFFIX_EXPLICIT" == "false" ]]; then TEMPLATE_SUFFIX="$LEGACY_TAG"; fi
    echo "[DEPRECATED] --tag は廃止予定です。--image-tag と --template-suffix を使ってください（次期メジャー版で削除）"
fi

# TEMPLATE_SUFFIX が空なら UnixTime を自動付与（再実行衝突回避 + ロールバック互換性）
if [[ -z "$TEMPLATE_SUFFIX" ]]; then
    TEMPLATE_SUFFIX="${IMAGE_TAG}-$(date +%s)"
fi

# .env 読み込みで INITIAL_DELAY が上書きされる前に args 値を退避
# 優先順位: 引数明示 > .env > スクリプトデフォルト(180)
ARG_INITIAL_DELAY="$INITIAL_DELAY"

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

# 引数明示なら args 値で復元（.env が上書きしていても args が勝つ）
# 引数なしなら .env の INITIAL_DELAY、それも無ければスクリプトデフォルト 180 が残る
if [[ "$INITIAL_DELAY_EXPLICIT" == "true" ]]; then
    INITIAL_DELAY="$ARG_INITIAL_DELAY"
fi

# 必須変数の確認
REQUIRED_VARS=("PROJECT_ID" "REGION" "ZONE" "REPO_NAME" "INSTANCE_GROUP_NAME" "INSTANCE_TEMPLATE_NAME" "GAME_SERVER_URL" "SECRET_UNITY_SERVER_AUTH")
for var in "${REQUIRED_VARS[@]}"; do
    if [[ -z "${!var}" ]]; then
        echo "[ERROR] Required variable $var is not set in .env"
        exit 1
    fi
done

# デフォルト値
MACHINE_TYPE="${MACHINE_TYPE:-e2-medium}"
UNITY_SERVER_PORT="${UNITY_SERVER_PORT:-7777}"
UNITY_SERVER_HEALTH_PORT="${UNITY_SERVER_HEALTH_PORT:-7778}"
NETWORK_TAG="${NETWORK_TAG:-unity-server}"
HEALTH_CHECK_NAME="${HEALTH_CHECK_NAME:-unity-server-health-check}"

IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}/unity-server"
TEMPLATE_NAME="${INSTANCE_TEMPLATE_NAME}-${TEMPLATE_SUFFIX}"

# Unity Server ビルド出力パス
BUILD_CONTEXT="${PROJECT_ROOT}/src/Game.Client/Builds/Server/Linux"

echo ""
echo "===== Deploy Configuration (Unity Server -> GCE) ====="
echo "PROJECT_ID:      $PROJECT_ID"
echo "REGION:          $REGION"
echo "ZONE:            $ZONE"
echo "IMAGE:           ${IMAGE}:${IMAGE_TAG}"
echo "TEMPLATE_NAME:   $TEMPLATE_NAME"
echo "INITIAL_DELAY:   $INITIAL_DELAY seconds"
echo "MACHINE_TYPE:    $MACHINE_TYPE"
echo "UNITY_SERVER_PORT:        $UNITY_SERVER_PORT (UDP)"
echo "UNITY_SERVER_HEALTH_PORT: $UNITY_SERVER_HEALTH_PORT (TCP)"
echo "INSTANCE_GROUP:  $INSTANCE_GROUP_NAME"
echo "NETWORK_TAG:     $NETWORK_TAG"
echo "BUILD_CONTEXT:   $BUILD_CONTEXT"
echo "GAME_SERVER_URL: $GAME_SERVER_URL"
echo "SECRET_NAME:     $SECRET_UNITY_SERVER_AUTH"
echo "======================================================="
echo ""

# 失敗時に gcloud の stderr を保全して表示するヘルパ。
# $1: 操作名（ログ表示用）
# $2..: 実行する gcloud コマンド
invoke_gcloud() {
    local description="$1"
    shift
    local captured
    captured="$("$@" 2>&1)"
    local rc=$?
    if [[ $rc -ne 0 ]]; then
        echo "[ERROR] $description failed (exit=$rc)" >&2
        echo "$captured" >&2
        exit $rc
    fi
    echo "$captured"
}

# インフラセットアップ（初回のみ）
if [[ "$SETUP_INFRA" == "true" ]]; then
    echo "[SETUP] Creating GCE infrastructure..."

    # ファイアウォールルール: UDP (ゲームトラフィック)
    echo "[SETUP] Creating firewall rule for game traffic (UDP ${UNITY_SERVER_PORT})..."
    gcloud compute firewall-rules create "${FIREWALL_RULE_GAME:-allow-unity-server-game}" \
        --network="${NETWORK:-default}" \
        --allow="udp:${UNITY_SERVER_PORT}" \
        --target-tags="${NETWORK_TAG}" \
        --description="Allow UDP game traffic to Unity Server" \
        --quiet 2>/dev/null || echo "  (firewall rule already exists)"

    # ファイアウォールルール: TCP (ヘルスチェック)
    echo "[SETUP] Creating firewall rule for health check (TCP ${UNITY_SERVER_HEALTH_PORT})..."
    gcloud compute firewall-rules create "${FIREWALL_RULE_HEALTH:-allow-unity-server-health}" \
        --network="${NETWORK:-default}" \
        --allow="tcp:${UNITY_SERVER_HEALTH_PORT}" \
        --source-ranges="35.191.0.0/16,130.211.0.0/22" \
        --target-tags="${NETWORK_TAG}" \
        --description="Allow GCE health check to Unity Server" \
        --quiet 2>/dev/null || echo "  (firewall rule already exists)"

    # ファイアウォールルール: TCP (Cloud Run Direct VPC Egress → DS 内部通信)
    VPC_CONNECTOR_SUBNET="${VPC_CONNECTOR_SUBNET:-10.10.0.0/26}"
    echo "[SETUP] Creating firewall rule for internal traffic (TCP ${UNITY_SERVER_HEALTH_PORT} from Direct VPC Egress ${VPC_CONNECTOR_SUBNET})..."
    gcloud compute firewall-rules create "${FIREWALL_RULE_INTERNAL:-allow-unity-server-internal}" \
        --network="${NETWORK:-default}" \
        --allow="tcp:${UNITY_SERVER_HEALTH_PORT}" \
        --source-ranges="${VPC_CONNECTOR_SUBNET}" \
        --target-tags="${NETWORK_TAG}" \
        --description="Allow Cloud Run Direct VPC Egress to send session/start to Unity Server" \
        --quiet 2>/dev/null || echo "  (firewall rule already exists)"

    # TCP ヘルスチェック作成
    echo "[SETUP] Creating TCP health check..."
    gcloud compute health-checks create tcp "${HEALTH_CHECK_NAME}" \
        --port="${UNITY_SERVER_HEALTH_PORT}" \
        --check-interval=10s \
        --timeout=5s \
        --healthy-threshold=2 \
        --unhealthy-threshold=3 \
        --quiet 2>/dev/null || echo "  (health check already exists)"

    # Secret Manager IAM
    echo "[SETUP] Granting Secret Manager access to GCE default service account..."
    gcloud secrets add-iam-policy-binding "${SECRET_UNITY_SERVER_AUTH}" \
        --member="serviceAccount:$(gcloud compute project-info describe --format='value(defaultServiceAccount)')" \
        --role="roles/secretmanager.secretAccessor" \
        --project="${PROJECT_ID}" \
        --quiet 2>/dev/null || echo "  (IAM binding may already exist)"

    echo "[SETUP] Infrastructure setup complete"
    echo ""
fi

# Docker 認証
echo "[1/5] Configuring Docker authentication..."
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet
if [[ $? -ne 0 ]]; then exit $?; fi

if [[ "$SKIP_BUILD" != "true" ]]; then
    if [[ ! -d "$BUILD_CONTEXT" ]]; then
        echo "[ERROR] Unity Server build output not found: $BUILD_CONTEXT"
        echo "[ERROR] Build the Unity Dedicated Server first:"
        echo "  Unity Editor -> Build > Server > Linux Dedicated Server"
        exit 1
    fi

    echo "[2/5] Building Docker image..."
    docker build -t "${IMAGE}:${IMAGE_TAG}" \
        -f "${PROJECT_ROOT}/docker/unity-server/Dockerfile" \
        "$BUILD_CONTEXT"
    if [[ $? -ne 0 ]]; then exit $?; fi

    echo "[3/5] Pushing to Artifact Registry..."
    docker push "${IMAGE}:${IMAGE_TAG}"
    if [[ $? -ne 0 ]]; then exit $?; fi
else
    echo "[2/5] Skipping build..."
    echo "[3/5] Skipping push..."
fi

if [[ "$BUILD_ONLY" != "true" ]]; then
    # image manifest 事前検証（fail-fast）
    # CI service account に roles/artifactregistry.reader が必要
    echo "[CHECK] Verifying image exists in Artifact Registry..."
    gcloud artifacts docker images describe "${IMAGE}:${IMAGE_TAG}" \
        --format="value(image_summary.digest)" >/dev/null 2>&1
    if [[ $? -ne 0 ]]; then
        echo "[ERROR] Image not found: ${IMAGE}:${IMAGE_TAG}"
        echo "[HINT] Run without --skip-build to build & push first,"
        echo "       or specify an existing --image-tag."
        echo "[HINT] CI service account requires roles/artifactregistry.reader."
        exit 1
    fi

    # Secret Manager から HMAC シークレット取得
    echo "[INFO] Fetching secret from Secret Manager (${SECRET_UNITY_SERVER_AUTH})..."
    UNITY_SERVER_AUTH_SESSION_SECRET=$(gcloud secrets versions access latest \
        --secret="${SECRET_UNITY_SERVER_AUTH}" \
        --project="${PROJECT_ID}")
    if [[ -z "$UNITY_SERVER_AUTH_SESSION_SECRET" ]]; then
        echo "[ERROR] Secret Manager から UNITY_SERVER_AUTH_SESSION_SECRET を取得できませんでした"
        exit 1
    fi

    # インスタンステンプレート作成
    echo "[4/5] Creating instance template ${TEMPLATE_NAME}..."
    invoke_gcloud "Create instance template" \
        gcloud compute instance-templates create-with-container "${TEMPLATE_NAME}" \
        --machine-type="${MACHINE_TYPE}" \
        --tags="${NETWORK_TAG}" \
        --container-image="${IMAGE}:${IMAGE_TAG}" \
        --container-env="UNITY_SERVER_AUTH_SESSION_SECRET=${UNITY_SERVER_AUTH_SESSION_SECRET}" \
        --container-env="GAME_SERVER_URL=${GAME_SERVER_URL}" \
        --container-env="UNITY_SERVER_PORT=${UNITY_SERVER_PORT}" \
        --container-env="UNITY_SERVER_HEALTH_PORT=${UNITY_SERVER_HEALTH_PORT}" \
        --container-arg="--port" \
        --container-arg="${UNITY_SERVER_PORT}" \
        --container-arg="--health-port" \
        --container-arg="${UNITY_SERVER_HEALTH_PORT}" \
        --scopes=https://www.googleapis.com/auth/cloud-platform \
        --region="${REGION}" >/dev/null

    # MIG 更新 or 作成
    echo "[5/5] Updating Managed Instance Group..."
    if gcloud compute instance-groups managed describe "${INSTANCE_GROUP_NAME}" \
        --zone="${ZONE}" >/dev/null 2>&1; then
        # 進行中の rolling-action がある場合は --force なしで中止
        IS_REACHED=$(gcloud compute instance-groups managed describe \
            "${INSTANCE_GROUP_NAME}" --zone="${ZONE}" \
            --format="value(status.versionTarget.isReached)" 2>/dev/null)
        if [[ "$IS_REACHED" == "False" && "$FORCE" != "true" ]]; then
            echo "[ERROR] Rolling update is already in progress."
            echo "[HINT] Wait for completion, or use --force to override (will disconnect active sessions)."
            exit 1
        fi

        invoke_gcloud "Set instance template" \
            gcloud compute instance-groups managed set-instance-template \
            "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --template="${TEMPLATE_NAME}" >/dev/null

        # autohealing initial-delay 更新（致命でない）
        gcloud compute instance-groups managed update \
            "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --initial-delay="${INITIAL_DELAY}" >/dev/null 2>&1 || true

        # rolling-action: --max-unavailable=0 と RESTART の衝突回避のため --minimal-action=replace を明示
        invoke_gcloud "Start rolling update" \
            gcloud compute instance-groups managed rolling-action start-update \
            "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --version="template=${TEMPLATE_NAME}" \
            --max-surge=1 \
            --max-unavailable=0 \
            --minimal-action=replace >/dev/null
    else
        # 新規 MIG を作成
        invoke_gcloud "Create new MIG" \
            gcloud compute instance-groups managed create "${INSTANCE_GROUP_NAME}" \
            --zone="${ZONE}" \
            --template="${TEMPLATE_NAME}" \
            --size=1 \
            --health-check="${HEALTH_CHECK_NAME}" \
            --initial-delay="${INITIAL_DELAY}" >/dev/null
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
    echo "Image: ${IMAGE}:${IMAGE_TAG}"
fi
