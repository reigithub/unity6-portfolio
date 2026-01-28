#!/bin/bash
# Unity Library キャッシュセットアップスクリプト
# Docker ボリュームを使用した永続キャッシュをシンボリックリンクで設定

set -e

# 引数チェック
if [ -z "$1" ]; then
    echo "Usage: $0 <unity-project-path>"
    echo "Example: $0 ./src/Game.Client"
    exit 1
fi

UNITY_PROJECT_PATH="$1"

# プロジェクト名をパスから抽出（最後のディレクトリ名）
PROJECT_NAME=$(basename "$UNITY_PROJECT_PATH")

# 永続キャッシュディレクトリ（Docker ボリューム内）
CACHE_DIR="/home/runner/.unity-library-cache/${PROJECT_NAME}"
mkdir -p "$CACHE_DIR"

# プロジェクトの Library ディレクトリ
LIBRARY_DIR="${UNITY_PROJECT_PATH}/Library"

echo "=== Unity Library Cache Setup ==="
echo "Project: ${PROJECT_NAME}"
echo "Cache Dir: ${CACHE_DIR}"
echo "Library Dir: ${LIBRARY_DIR}"

# 既存の Library フォルダがある場合（シンボリックリンクではない）
if [ -d "$LIBRARY_DIR" ] && [ ! -L "$LIBRARY_DIR" ]; then
    # キャッシュが空なら既存を移動
    if [ -z "$(ls -A "$CACHE_DIR" 2>/dev/null)" ]; then
        echo "Moving existing Library to cache..."
        mv "$LIBRARY_DIR"/* "$CACHE_DIR/" 2>/dev/null || true
    fi
    rm -rf "$LIBRARY_DIR"
fi

# シンボリックリンク作成
if [ ! -L "$LIBRARY_DIR" ]; then
    ln -s "$CACHE_DIR" "$LIBRARY_DIR"
    echo "Created symlink: $LIBRARY_DIR -> $CACHE_DIR"
else
    echo "Symlink already exists"
fi

# キャッシュ状態を表示
echo "=== Cache Status ==="
if [ -n "$(ls -A "$CACHE_DIR" 2>/dev/null)" ]; then
    du -sh "$CACHE_DIR" 2>/dev/null
    echo "Files: $(ls "$CACHE_DIR" | wc -l)"
else
    echo "Cache is empty (first run)"
fi
