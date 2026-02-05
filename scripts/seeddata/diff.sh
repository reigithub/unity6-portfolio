#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Diff TSV Directories"
echo "========================================"
echo

SOURCE_DIR=${1:-"masterdata/raw/"}
TARGET_DIR=${2:-"masterdata/dump/"}

echo "Source: $SOURCE_DIR"
echo "Target: $TARGET_DIR"
echo

dotnet run --project src/Game.Tools -- seeddata diff \
  --source-dir "$SOURCE_DIR" \
  --target-dir "$TARGET_DIR"

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] All files match."
else
    echo "[WARNING] Differences found."
fi
