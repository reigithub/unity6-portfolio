@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Seed Database from TSV
echo ========================================
echo.

dotnet run --project src/Game.Tools -- seeddata seed ^
  --tsv-dir masterdata/raw/

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Database seeded successfully.
) else (
    echo [ERROR] Seed failed with error code %ERRORLEVEL%
)
echo.
pause
