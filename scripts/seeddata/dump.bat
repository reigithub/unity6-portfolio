@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Dump Database to TSV
echo ========================================
echo.

dotnet run --project src/Game.Tools -- seeddata dump ^
  --out-dir masterdata/dump/

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Database dumped to masterdata/dump/
) else (
    echo [ERROR] Dump failed with error code %ERRORLEVEL%
)
echo.
pause
