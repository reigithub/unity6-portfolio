#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Database Migration - Up"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- migrate up

echo
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Migrations applied successfully."
else
    echo "[ERROR] Migration failed."
fi
