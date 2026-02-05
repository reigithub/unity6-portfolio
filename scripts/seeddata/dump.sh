#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Dump Database to TSV"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- seeddata dump \
  --out-dir masterdata/dump/

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Database dumped to masterdata/dump/"
else
    echo "[ERROR] Dump failed."
fi
