#!/bin/bash
# Unity CI/CD 設定読み込みスクリプト
# このスクリプトは GitHub Actions ワークフローから呼び出されます

set -e

CONFIG_FILE=".github/unity-ci.config"

echo "Loading configuration from ${CONFIG_FILE}..."

# .env ファイルから設定を読み込み
if [ -f "$CONFIG_FILE" ]; then
    # コメント行と空行を除外して環境変数として設定
    while IFS='=' read -r key value || [ -n "$key" ]; do
        # コメント行と空行をスキップ
        [[ "$key" =~ ^[[:space:]]*# ]] && continue
        [[ -z "$key" ]] && continue

        # キーと値の前後の空白を除去
        key=$(echo "$key" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
        value=$(echo "$value" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')

        # 空のキーをスキップ
        [[ -z "$key" ]] && continue

        echo "${key}=${value}" >> $GITHUB_ENV
        echo "  ${key}=${value}"
    done < "$CONFIG_FILE"
else
    echo "Warning: Configuration file not found: ${CONFIG_FILE}"
    echo "Using default values..."
fi

# workflow_dispatch のオーバーライドを適用
if [ -n "${INPUT_PROJECT_PATH}" ]; then
    echo "UNITY_PROJECT_PATH=${INPUT_PROJECT_PATH}" >> $GITHUB_ENV
    echo "  UNITY_PROJECT_PATH=${INPUT_PROJECT_PATH} (overridden)"
fi

# デフォルト値を設定（未設定の場合のみ）
# 注: GITHUB_ENV に追加された値は現在のステップでは参照できないため、
#     ここでは既存の環境変数のみをチェック
{
    echo "UNITY_PROJECT_PATH=${UNITY_PROJECT_PATH:-./src/Game.Client}"
    echo "CACHE_KEY_PREFIX=${CACHE_KEY_PREFIX:-Library}"
    echo "ARTIFACT_RETENTION_DAYS=${ARTIFACT_RETENTION_DAYS:-14}"
    echo "BUILD_RETENTION_DAYS=${BUILD_RETENTION_DAYS:-30}"
    echo "DEFAULT_BUILD_TARGET=${DEFAULT_BUILD_TARGET:-StandaloneWindows64}"
} >> $GITHUB_ENV

echo "Configuration loaded successfully."
