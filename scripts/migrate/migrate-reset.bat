@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Database Migration - Reset
echo ========================================
echo.
echo WARNING: This will DROP all tables and re-create them!
echo.

set /p CONFIRM="Are you sure? (y/N): "
if /i not "%CONFIRM%"=="y" (
    echo Aborted.
    pause
    exit /b 0
)

set /p SEED="Re-seed master data after reset? (y/N): "
if /i "%SEED%"=="y" (
    dotnet run --project src/Game.Tools -- migrate reset --force --version 9999999999 --seed
) else (
    dotnet run --project src/Game.Tools -- migrate reset --force --version 9999999999
)

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Database reset completed.
) else (
    echo [ERROR] Reset failed with error code %ERRORLEVEL%
)
echo.
pause
