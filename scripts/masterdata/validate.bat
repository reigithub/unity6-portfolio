@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   MasterData Validate (TSV Check)
echo ========================================
echo.

dotnet run --project src/Game.Tools -- masterdata validate ^
  --tsv-dir masterdata/raw/ ^
  --proto-dir masterdata/proto/

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] All TSV files are valid.
) else (
    echo [ERROR] Validation failed with error code %ERRORLEVEL%
)
echo.
pause
