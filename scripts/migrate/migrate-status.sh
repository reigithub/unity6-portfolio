#!/bin/bash
cd "$(dirname "$0")/../.."

echo "========================================"
echo "  Database Migration - Status"
echo "========================================"
echo

dotnet run --project src/Game.Tools -- migrate status
