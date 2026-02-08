#!/bin/bash
# Unity Accelerator 接続引数を出力するスクリプト
#
# 使用方法:
#   ACCELERATOR_ARGS=$(source .github/scripts/unity-accelerator-args.sh)
#   unity-editor $ACCELERATOR_ARGS -batchmode ...
#
# または GitHub Actions で:
#   - name: Get Accelerator args
#     id: accelerator
#     run: |
#       source .github/scripts/unity-accelerator-args.sh
#       echo "args=$ACCELERATOR_ARGS" >> $GITHUB_OUTPUT

# Accelerator のエンドポイント（環境変数またはデフォルト値）
ACCELERATOR_HOST="${UNITY_ACCELERATOR_ENDPOINT:-unity-accelerator:10080}"

# 接続確認
if curl -s --max-time 5 "http://${ACCELERATOR_HOST}" > /dev/null 2>&1; then
    echo "Unity Accelerator is available at ${ACCELERATOR_HOST}" >&2
    ACCELERATOR_ARGS="-cacheServerEndpoint ${ACCELERATOR_HOST} -adb2 -EnableCacheServer"
else
    echo "Unity Accelerator is not available at ${ACCELERATOR_HOST}, proceeding without cache server" >&2
    ACCELERATOR_ARGS=""
fi

# 結果を出力（スクリプトの戻り値として使用可能）
echo "$ACCELERATOR_ARGS"
