@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Database Migration - Status
echo ========================================
echo.

dotnet run --project src/Game.Tools -- migrate status

echo.
pause
