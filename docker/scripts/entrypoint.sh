#!/bin/bash
# GitHub Actions Self-hosted Runner Entrypoint (GitHub App 版)
# GitHub App を使用してインストールアクセストークンを取得し、Runner を登録します

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1" >&2
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

# X11 ディレクトリのセットアップ（xvfb-run 用）
log_info "Setting up X11 socket directory..."
mkdir -p /tmp/.X11-unix
chmod 1777 /tmp/.X11-unix
chown root:root /tmp/.X11-unix
log_info "X11 socket directory configured"

# Validate required environment variables
if [ -z "$GITHUB_REPOSITORY" ]; then
    log_error "GITHUB_REPOSITORY is required (e.g., owner/repo)"
    exit 1
fi

# 認証方式の判定
USE_GITHUB_APP=false
if [ -n "$GITHUB_APP_ID" ] && [ -n "$GITHUB_APP_INSTALLATION_ID" ]; then
    if [ -n "$GITHUB_APP_PRIVATE_KEY" ] || [ -n "$GITHUB_APP_PRIVATE_KEY_BASE64" ] || [ -n "$GITHUB_APP_PRIVATE_KEY_FILE" ]; then
        USE_GITHUB_APP=true
    fi
fi

if [ "$USE_GITHUB_APP" = false ] && [ -z "$GITHUB_TOKEN" ]; then
    log_error "Authentication required. Provide either:"
    log_error "  - GITHUB_TOKEN (Personal Access Token)"
    log_error "  - GITHUB_APP_ID + GITHUB_APP_INSTALLATION_ID + GITHUB_APP_PRIVATE_KEY"
    exit 1
fi

# Optional variables with defaults
RUNNER_NAME=${RUNNER_NAME:-"unity-runner-$(hostname)"}
RUNNER_LABELS=${RUNNER_LABELS:-"self-hosted,linux,unity,docker"}
RUNNER_WORKDIR=${RUNNER_WORKDIR:-"/home/runner/actions-runner/_work"}

log_info "=========================================="
log_info "GitHub Actions Runner Setup"
log_info "=========================================="
log_info "Repository: $GITHUB_REPOSITORY"
log_info "Runner Name: $RUNNER_NAME"
log_info "Labels: $RUNNER_LABELS"
log_info "Work Directory: $RUNNER_WORKDIR"
if [ "$USE_GITHUB_APP" = true ]; then
    log_info "Auth Method: GitHub App (ID: $GITHUB_APP_ID)"
else
    log_info "Auth Method: Personal Access Token"
fi
log_info "=========================================="

# トークンの取得
get_github_token() {
    if [ "$USE_GITHUB_APP" = true ]; then
        log_info "Generating token from GitHub App..."
        /github-app-token.sh
    else
        echo "$GITHUB_TOKEN"
    fi
}

# Setup Unity license if provided
# Search for any .ulf file in /unity-license directory
LICENSE_FILE=$(find /unity-license -name "*.ulf" -type f 2>/dev/null | head -n 1)

if [ -n "$LICENSE_FILE" ]; then
    log_info "Unity license file found: $LICENSE_FILE"
    LICENSE_DIR="/home/runner/.local/share/unity3d/Unity"

    # ディレクトリ作成（存在しない場合）
    if [ ! -d "$LICENSE_DIR" ]; then
        log_info "Creating license directory: $LICENSE_DIR"
        mkdir -p "$LICENSE_DIR" || {
            log_error "Failed to create directory: $LICENSE_DIR"
            exit 1
        }
    fi

    # ライセンスファイルをコピー
    if cp "$LICENSE_FILE" "$LICENSE_DIR/Unity_lic.ulf"; then
        chmod 600 "$LICENSE_DIR/Unity_lic.ulf"
        log_info "Unity license configured successfully"
        log_info "License file: $LICENSE_DIR/Unity_lic.ulf"
    else
        log_error "Failed to copy license file"
        exit 1
    fi
elif [ -n "$UNITY_SERIAL" ] && [ -n "$UNITY_EMAIL" ] && [ -n "$UNITY_PASSWORD" ]; then
    log_info "Activating Unity with serial key..."
    unity-editor \
        -batchmode \
        -nographics \
        -quit \
        -serial "$UNITY_SERIAL" \
        -username "$UNITY_EMAIL" \
        -password "$UNITY_PASSWORD" \
        -logFile /dev/stdout || true
    log_info "Unity activation attempted"
else
    log_warn "No Unity license found!"
    log_warn "Place your .ulf license file in the unity-license directory on the host"
fi

# Get GitHub token
CURRENT_TOKEN=$(get_github_token)
if [ -z "$CURRENT_TOKEN" ]; then
    log_error "Failed to obtain GitHub token"
    exit 1
fi

# Get registration token from GitHub API
log_info "Requesting runner registration token..."
log_info "Token length: ${#CURRENT_TOKEN}"
log_info "Token prefix: ${CURRENT_TOKEN:0:10}..."

API_URL="https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runners/registration-token"
log_info "API URL: ${API_URL}"
log_info "Starting curl request..."

# curl を実行（HTTP/1.1 を強制、タイムアウト設定付き）
REG_RESPONSE=""
if REG_RESPONSE=$(curl --http1.1 --silent --show-error --max-time 30 --connect-timeout 10 \
    -X POST \
    -H "Authorization: token ${CURRENT_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    "${API_URL}" 2>&1); then
    log_info "curl completed successfully"
else
    log_error "curl failed with exit code: $?"
    log_error "Response: ${REG_RESPONSE}"
fi

log_info "API Response received"
log_info "Response length: ${#REG_RESPONSE} bytes"
log_info "Response preview: ${REG_RESPONSE:0:100}..."

REG_TOKEN=$(echo "$REG_RESPONSE" | jq -r .token 2>/dev/null)

log_info "Token extraction completed"

if [ "$REG_TOKEN" == "null" ] || [ -z "$REG_TOKEN" ]; then
    log_error "Failed to get registration token!"
    log_error "API Response: $REG_RESPONSE"
    log_error ""
    log_error "Please check:"
    log_error "  1. GitHub App has 'Administration: Read and write' permission"
    log_error "  2. GitHub App is installed on repository: ${GITHUB_REPOSITORY}"
    log_error "  3. Repository name is correct (owner/repo format)"
    exit 1
fi

log_info "Registration token obtained successfully"

# Configure runner (if not already configured)
if [ ! -f ".runner" ]; then
    log_info "Configuring runner..."
    ./config.sh \
        --url "https://github.com/${GITHUB_REPOSITORY}" \
        --token "${REG_TOKEN}" \
        --name "${RUNNER_NAME}" \
        --labels "${RUNNER_LABELS}" \
        --work "${RUNNER_WORKDIR}" \
        --unattended \
        --replace
    log_info "Runner configured successfully"
else
    log_info "Runner already configured, skipping configuration"
fi

# Cleanup function for graceful shutdown
cleanup() {
    log_info "=========================================="
    log_info "Caught signal, removing runner..."
    log_info "=========================================="

    # Get a fresh token for removal
    REMOVE_TOKEN_AUTH=$(get_github_token)
    REMOVE_TOKEN=$(curl --http1.1 -s -X POST \
        -H "Authorization: token ${REMOVE_TOKEN_AUTH}" \
        -H "Accept: application/vnd.github+json" \
        "https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runners/registration-token" \
        | jq -r .token)

    if [ "$REMOVE_TOKEN" != "null" ] && [ -n "$REMOVE_TOKEN" ]; then
        ./config.sh remove --token "${REMOVE_TOKEN}" || true
        log_info "Runner removed from GitHub"
    else
        log_warn "Could not get removal token, runner may remain registered"
    fi

    exit 0
}

# Trap signals for graceful shutdown
trap cleanup SIGTERM SIGINT SIGQUIT

log_info "=========================================="
log_info "Starting GitHub Actions Runner..."
log_info "=========================================="

# Start the runner
./run.sh &

# Wait for runner process
wait $!
