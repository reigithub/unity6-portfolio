#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Seed Database from TSV"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- seeddata seed \
  --tsv-dir masterdata/raw/

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Database seeded successfully."
else
    echo "[ERROR] Seed failed."
fi
