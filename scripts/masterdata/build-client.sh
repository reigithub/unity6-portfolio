#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

echo "========================================"
echo "  MasterData Build (Client)"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- masterdata build \
  --tsv-dir masterdata/raw/ \
  --proto-dir masterdata/proto/ \
  --out-client src/Game.Client/Assets/MasterData/MasterDataBinary.bytes

echo
echo "[SUCCESS] Client binary generated."
