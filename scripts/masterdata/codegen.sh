#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

echo "========================================"
echo "  MasterData Codegen (Proto -> C#)"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- masterdata codegen \
  --proto-dir masterdata/proto/ \
  --out-client src/Game.Client/Assets/Programs/Runtime/Shared/MasterData/ \
  --out-server src/Game.Server/MasterData/

echo
echo "[SUCCESS] C# classes generated."
