#!/bin/bash
# Unity CI ホストディレクトリセットアップスクリプト
#
# このスクリプトは、Docker ホストマシン上で実行してください。
# Unity Accelerator と Library キャッシュ用のディレクトリを作成します。
#
# 使用方法:
#   sudo ./setup-host-directories.sh
#
# カスタムパスを指定する場合:
#   sudo CACHE_BASE_PATH=/custom/path ./setup-host-directories.sh

set -e

# ベースパス（デフォルト: /var/lib/unity-ci）
CACHE_BASE_PATH="${CACHE_BASE_PATH:-/var/lib/unity-ci}"

# runner ユーザーの UID/GID（Docker コンテナ内と一致させる）
RUNNER_UID="${RUNNER_UID:-1000}"
RUNNER_GID="${RUNNER_GID:-1000}"

echo "=== Unity CI Host Directory Setup ==="
echo "Base path: ${CACHE_BASE_PATH}"
echo "Owner: ${RUNNER_UID}:${RUNNER_GID}"
echo ""

# ディレクトリ作成
echo "Creating directories..."

# Unity Accelerator データディレクトリ
mkdir -p "${CACHE_BASE_PATH}/accelerator"
echo "  Created: ${CACHE_BASE_PATH}/accelerator"

# Library キャッシュディレクトリ
mkdir -p "${CACHE_BASE_PATH}/library-cache"
echo "  Created: ${CACHE_BASE_PATH}/library-cache"

# Unity グローバルキャッシュディレクトリ
mkdir -p "${CACHE_BASE_PATH}/unity-cache"
echo "  Created: ${CACHE_BASE_PATH}/unity-cache"

# 権限設定
echo ""
echo "Setting permissions..."
chown -R "${RUNNER_UID}:${RUNNER_GID}" "${CACHE_BASE_PATH}"
chmod -R 755 "${CACHE_BASE_PATH}"

# 結果表示
echo ""
echo "=== Setup Complete ==="
echo ""
echo "Directory structure:"
ls -la "${CACHE_BASE_PATH}"
echo ""
echo "Disk usage:"
df -h "${CACHE_BASE_PATH}"
echo ""
echo "Next steps:"
echo "  1. Update .env file with the following paths:"
echo "     ACCELERATOR_DATA_PATH=${CACHE_BASE_PATH}/accelerator"
echo "     LIBRARY_CACHE_PATH=${CACHE_BASE_PATH}/library-cache"
echo "     UNITY_CACHE_PATH=${CACHE_BASE_PATH}/unity-cache"
echo ""
echo "  2. Start the containers:"
echo "     docker compose up -d"
echo ""
echo "  3. Access Accelerator dashboard:"
echo "     http://localhost:8080/dashboard"
echo ""
