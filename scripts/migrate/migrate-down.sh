#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Database Migration - Down (Rollback)"
echo "========================================"
echo

STEPS=${1:-1}
echo "Rollback steps: $STEPS"

dotnet run --project src/Game.Tools -- migrate down --steps "$STEPS"

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Rolled back $STEPS migration(s)."
else
    echo "[ERROR] Rollback failed."
fi
