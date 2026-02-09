#!/bin/sh
# Unity Accelerator Custom Entrypoint
#
# - データディレクトリの初期化
# - パーミッション設定
# - 公式エントリポイントの実行

set -e

DATA_DIR="${UNITY_ACCELERATOR_PERSIST:-/agent}"

echo "=== Unity Accelerator Custom Entrypoint ==="
echo "Data directory: ${DATA_DIR}"

# データディレクトリの初期化
if [ ! -d "${DATA_DIR}" ]; then
    echo "Creating data directory: ${DATA_DIR}"
    mkdir -p "${DATA_DIR}"
fi

# 必要なサブディレクトリを作成
for subdir in cachedb cachedbpending log runtime; do
    if [ ! -d "${DATA_DIR}/${subdir}" ]; then
        echo "Creating subdirectory: ${DATA_DIR}/${subdir}"
        mkdir -p "${DATA_DIR}/${subdir}"
    fi
done

# パーミッション設定（書き込み可能にする）
chmod -R 755 "${DATA_DIR}" 2>/dev/null || true

echo "Data directory initialized successfully"
echo "==========================================="

# 公式エントリポイントを実行
exec accelerator-entrypoint "$@"
