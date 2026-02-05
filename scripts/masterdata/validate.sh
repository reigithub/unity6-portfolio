#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

echo "========================================"
echo "  MasterData Validate (TSV Check)"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- masterdata validate \
  --tsv-dir masterdata/raw/ \
  --proto-dir masterdata/proto/

echo
echo "[SUCCESS] All TSV files are valid."
