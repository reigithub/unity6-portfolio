#!/bin/bash
# ============================================
# Unity プロジェクトのフォーマットチェック
# ============================================
#
# Usage:
#   ./format-check.sh              # チェックのみ（Assets/Programs/ のみ）
#   ./format-check.sh --fix        # 自動修正
#   ./format-check.sh --all        # 全ファイルをチェック
#   ./format-check.sh --fix --all  # 全ファイルを自動修正
#
# ============================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY_PROJECT_PATH="$SCRIPT_DIR/../src/Game.Client"

# 引数解析
FIX_MODE=false
ALL_MODE=false
for arg in "$@"; do
    case $arg in
        --fix|-f)
            FIX_MODE=true
            ;;
        --all|-a)
            ALL_MODE=true
            ;;
    esac
done

# 対象パス設定
INCLUDE_PATH="Assets/Programs/"
if [ "$ALL_MODE" = true ]; then
    INCLUDE_OPTION=""
    echo "Target: All files"
else
    INCLUDE_OPTION="--include $INCLUDE_PATH"
    echo "Target: $INCLUDE_PATH"
fi

echo "=== Unity Project Format Check ==="
echo "Project: $UNITY_PROJECT_PATH"
echo ""

# ディレクトリ移動
cd "$UNITY_PROJECT_PATH"

# .sln ファイルを検索
SLN_FILE=""
if [ -f "Game.Client.sln" ]; then
    SLN_FILE="Game.Client.sln"
else
    SLN_FILE=$(find . -maxdepth 1 -name "*.sln" | head -1)
fi

if [ -z "$SLN_FILE" ]; then
    echo "No .sln file found. Please open Unity Editor to generate the solution file."
    echo ""
    echo "In Unity Editor:"
    echo "  Edit -> Preferences -> External Tools -> Regenerate project files"
    exit 1
fi

echo "Using solution: $SLN_FILE"
echo ""

# dotnet format 実行
if [ "$FIX_MODE" = true ]; then
    echo "Running: dotnet format \"$SLN_FILE\" $INCLUDE_OPTION"
    eval dotnet format "$SLN_FILE" $INCLUDE_OPTION --verbosity normal

    if [ $? -eq 0 ]; then
        echo ""
        echo "Format completed successfully!"
    else
        echo ""
        echo "Format completed with warnings."
    fi
else
    echo "Running: dotnet format \"$SLN_FILE\" --verify-no-changes $INCLUDE_OPTION"

    if eval dotnet format "$SLN_FILE" --verify-no-changes $INCLUDE_OPTION --verbosity normal; then
        echo ""
        echo "All files are properly formatted!"
    else
        echo ""
        echo "Some files need formatting. Run with --fix to auto-fix."
        exit 1
    fi
fi
