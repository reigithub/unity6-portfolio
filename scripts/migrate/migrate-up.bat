@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Database Migration - Up
echo ========================================
echo.

dotnet run --project src/Game.Tools -- migrate up

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Migrations applied successfully.
) else (
    echo [ERROR] Migration failed with error code %ERRORLEVEL%
)
echo.
pause
