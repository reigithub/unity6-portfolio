#!/bin/bash

# Cloudflare R2 アップロードスクリプト（macOS/Linux用）
# Usage: ./upload-to-r2.sh [Platform] [--dry-run]

set -e

# 設定
BUILD_PATH="ServerData"
BUCKET_NAME="unity6-portfolio"
RCLONE_REMOTE="r2"
TRANSFERS=8
CHECKERS=16
CUSTOM_DOMAIN="rei-unity6-portfolio.com"

# スクリプトのディレクトリを取得
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
CLIENT_PATH="$PROJECT_ROOT/src/Game.Client"
FULL_BUILD_PATH="$CLIENT_PATH/$BUILD_PATH"

# 色定義
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# プラットフォーム一覧
PLATFORMS=(
    "StandaloneWindows64"
    "StandaloneOSX"
    "StandaloneLinux64"
    "Android"
    "iOS"
    "WebGL"
)

# 引数処理
PLATFORM=""
DRY_RUN=""
VERBOSE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run|-d)
            DRY_RUN="--dry-run"
            shift
            ;;
        --verbose|-v)
            VERBOSE="--verbose"
            shift
            ;;
        *)
            PLATFORM="$1"
            shift
            ;;
    esac
done

# rclone が利用可能か確認
if ! command -v rclone &> /dev/null; then
    echo -e "${RED}ERROR: rclone が見つかりません。${NC}"
    echo ""
    echo -e "${YELLOW}インストール方法:${NC}"
    echo "  macOS: brew install rclone"
    echo "  Linux: curl https://rclone.org/install.sh | sudo bash"
    echo ""
    echo "インストール後、rclone config で R2 の設定を行ってください。"
    exit 1
fi

# リモート設定の確認
if ! rclone listremotes | grep -q "^${RCLONE_REMOTE}:$"; then
    echo -e "${RED}ERROR: rclone リモート '$RCLONE_REMOTE' が設定されていません。${NC}"
    echo ""
    echo -e "${YELLOW}設定方法:${NC}"
    echo "  rclone config"
    exit 1
fi

# ビルドパスの確認
if [ ! -d "$FULL_BUILD_PATH" ]; then
    echo -e "${RED}ERROR: ビルドパスが存在しません: $FULL_BUILD_PATH${NC}"
    echo ""
    echo "Addressablesをビルドしてから再度実行してください。"
    exit 1
fi

# ターゲットプラットフォームを決定
if [ -n "$PLATFORM" ]; then
    TARGET_PLATFORMS=("$PLATFORM")
else
    TARGET_PLATFORMS=("${PLATFORMS[@]}")
fi

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN} Cloudflare R2 アップロード${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo "Bucket:     $BUCKET_NAME"
echo "Build Path: $FULL_BUILD_PATH"
echo "Remote:     ${RCLONE_REMOTE}:${BUCKET_NAME}"
if [ -n "$DRY_RUN" ]; then
    echo -e "${YELLOW}Mode:       DRY RUN (実際にはアップロードしません)${NC}"
fi
echo ""

uploaded=0
skipped=0
failed=0
total_files=0

for platform in "${TARGET_PLATFORMS[@]}"; do
    source_path="$FULL_BUILD_PATH/$platform"

    if [ ! -d "$source_path" ]; then
        echo -e "${YELLOW}[$platform] スキップ - ビルドが存在しません${NC}"
        ((skipped++))
        continue
    fi

    file_count=$(find "$source_path" -type f | wc -l | tr -d ' ')
    folder_size=$(du -sh "$source_path" 2>/dev/null | cut -f1)

    echo -e "${GREEN}[$platform] アップロード中...${NC}"
    echo "  Files: $file_count, Size: $folder_size"

    destination="${RCLONE_REMOTE}:${BUCKET_NAME}/${platform}"

    rclone_args=(
        "sync"
        "$source_path"
        "$destination"
        "--transfers" "$TRANSFERS"
        "--checkers" "$CHECKERS"
        "--progress"
    )

    if [ -n "$VERBOSE" ]; then
        rclone_args+=("--verbose")
    else
        rclone_args+=("--stats-one-line" "--stats" "5s")
    fi

    if [ -n "$DRY_RUN" ]; then
        rclone_args+=("--dry-run")
    fi

    if rclone "${rclone_args[@]}"; then
        echo -e "${GREEN}[$platform] 完了 ✓${NC}"
        ((uploaded++))
        ((total_files += file_count))
    else
        echo -e "${RED}[$platform] エラー ✗${NC}"
        ((failed++))
    fi

    echo ""
done

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN} 結果${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo -e "  成功:     ${GREEN}$uploaded プラットフォーム${NC}"
echo -e "  スキップ: ${YELLOW}$skipped プラットフォーム${NC}"
if [ $failed -gt 0 ]; then
    echo -e "  失敗:     ${RED}$failed プラットフォーム${NC}"
else
    echo "  失敗:     $failed プラットフォーム"
fi
echo ""

# URLを表示
if [ $uploaded -gt 0 ] && [ -z "$DRY_RUN" ]; then
    echo -e "${YELLOW}アップロード先URL:${NC}"
    for platform in "${TARGET_PLATFORMS[@]}"; do
        source_path="$FULL_BUILD_PATH/$platform"
        if [ -d "$source_path" ]; then
            echo "  https://${CUSTOM_DOMAIN}/$platform/"
        fi
    done
    echo ""
fi

if [ $failed -gt 0 ]; then
    exit 1
fi

exit 0
