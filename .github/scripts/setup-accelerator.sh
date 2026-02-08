#!/bin/bash
# Unity Accelerator 接続確認スクリプト
# 使用方法: source .github/scripts/setup-accelerator.sh
#
# このスクリプトを source すると、以下の環境変数が設定されます:
#   ACCELERATOR_ARGS - Unity コマンドに渡す引数
#
# 例:
#   source .github/scripts/setup-accelerator.sh
#   unity-editor ... $ACCELERATOR_ARGS ...

ACCELERATOR_ENDPOINT="${UNITY_ACCELERATOR_ENDPOINT:-unity-accelerator:10080}"

if curl -s --max-time 5 "http://${ACCELERATOR_ENDPOINT}" > /dev/null 2>&1; then
    echo "Unity Accelerator is available at ${ACCELERATOR_ENDPOINT}"
    export ACCELERATOR_ARGS="-cacheServerEndpoint ${ACCELERATOR_ENDPOINT} -adb2 -EnableCacheServer"
else
    echo "Unity Accelerator is not available, proceeding without cache server"
    export ACCELERATOR_ARGS=""
fi
