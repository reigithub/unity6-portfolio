#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Database Migration - Reset"
echo "========================================"
echo
echo "WARNING: This will DROP all tables and re-create them!"
echo

read -p "Are you sure? (y/N): " CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

read -p "Re-seed master data after reset? (y/N): " SEED
if [[ "$SEED" =~ ^[Yy]$ ]]; then
    dotnet run --project src/Game.Tools -- migrate reset --force --version 9999999999 --seed
else
    dotnet run --project src/Game.Tools -- migrate reset --force --version 9999999999
fi

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Database reset completed."
else
    echo "[ERROR] Reset failed."
fi
