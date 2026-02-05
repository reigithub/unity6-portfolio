@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Database Migration - Down (Rollback)
echo ========================================
echo.

set /p STEPS="Rollback steps (default: 1): "
if "%STEPS%"=="" set STEPS=1

dotnet run --project src/Game.Tools -- migrate down --steps %STEPS%

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Rolled back %STEPS% migration(s).
) else (
    echo [ERROR] Rollback failed with error code %ERRORLEVEL%
)
echo.
pause
