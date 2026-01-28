#!/bin/bash
# Unity CI/CD 設定読み込みスクリプト
# このスクリプトは GitHub Actions ワークフローから呼び出されます

set -e

CONFIG_FILE=".github/unity-ci.ini"

# 必須の設定キー
REQUIRED_KEYS=(
    "UNITY_PROJECT_PATH"
    "CACHE_KEY_PREFIX"
    "ARTIFACT_RETENTION_DAYS"
    "BUILD_RETENTION_DAYS"
    "DEFAULT_BUILD_TARGET"
)

echo "Loading configuration from ${CONFIG_FILE}..."

# 設定ファイルの存在確認
if [ ! -f "$CONFIG_FILE" ]; then
    echo "ERROR: Configuration file not found: ${CONFIG_FILE}"
    exit 1
fi

# 設定値を読み込み
while IFS='=' read -r key value || [ -n "$key" ]; do
    # コメント行と空行をスキップ
    [[ "$key" =~ ^[[:space:]]*# ]] && continue
    [[ -z "$key" ]] && continue

    # キーと値の前後の空白を除去
    key=$(echo "$key" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
    value=$(echo "$value" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')

    # 空のキーをスキップ
    [[ -z "$key" ]] && continue

    # 変数に代入
    declare "$key=$value"
    echo "  ${key}=${value}"
done < "$CONFIG_FILE"

# workflow_dispatch のオーバーライドを適用
if [ -n "${INPUT_PROJECT_PATH}" ]; then
    UNITY_PROJECT_PATH="${INPUT_PROJECT_PATH}"
    echo "  UNITY_PROJECT_PATH=${INPUT_PROJECT_PATH} (overridden)"
fi

# 必須キーの検証
echo ""
echo "Validating required configuration..."
missing_keys=()
for key in "${REQUIRED_KEYS[@]}"; do
    if [ -z "${!key}" ]; then
        missing_keys+=("$key")
    fi
done

if [ ${#missing_keys[@]} -gt 0 ]; then
    echo "ERROR: Missing required configuration keys:"
    for key in "${missing_keys[@]}"; do
        echo "  - ${key}"
    done
    exit 1
fi

echo "All required configuration keys are present."

# GITHUB_ENV に書き込み（同一ジョブ内の後続ステップ用）
if [ -z "$GITHUB_ENV" ]; then
    echo "ERROR: GITHUB_ENV is not set. This script must be run in GitHub Actions."
    exit 1
fi

{
    echo "UNITY_PROJECT_PATH=${UNITY_PROJECT_PATH}"
    echo "CACHE_KEY_PREFIX=${CACHE_KEY_PREFIX}"
    echo "ARTIFACT_RETENTION_DAYS=${ARTIFACT_RETENTION_DAYS}"
    echo "BUILD_RETENTION_DAYS=${BUILD_RETENTION_DAYS}"
    echo "DEFAULT_BUILD_TARGET=${DEFAULT_BUILD_TARGET}"
} >> "$GITHUB_ENV"

# GITHUB_OUTPUT に書き込み（ジョブ間の出力用）
if [ -z "$GITHUB_OUTPUT" ]; then
    echo "ERROR: GITHUB_OUTPUT is not set. This script must be run in GitHub Actions."
    exit 1
fi

{
    echo "UNITY_PROJECT_PATH=${UNITY_PROJECT_PATH}"
    echo "CACHE_KEY_PREFIX=${CACHE_KEY_PREFIX}"
    echo "ARTIFACT_RETENTION_DAYS=${ARTIFACT_RETENTION_DAYS}"
    echo "BUILD_RETENTION_DAYS=${BUILD_RETENTION_DAYS}"
    echo "DEFAULT_BUILD_TARGET=${DEFAULT_BUILD_TARGET}"
} >> "$GITHUB_OUTPUT"

echo ""
echo "Configuration loaded successfully."
