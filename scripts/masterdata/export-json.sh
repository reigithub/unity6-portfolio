#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

echo "========================================"
echo "  MasterData Export (Binary -> JSON)"
echo "========================================"
echo

mkdir -p export

dotnet run --project src/Game.Tools -- masterdata export json \
  --input src/Game.Server/MasterData/masterdata.bytes \
  --out-dir export/ \
  --target server

echo
echo "[SUCCESS] Exported to export/ directory."
