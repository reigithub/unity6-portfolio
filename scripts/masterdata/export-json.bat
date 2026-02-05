@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   MasterData Export (Binary to JSON)
echo ========================================
echo.

if not exist "export" mkdir export

dotnet run --project src/Game.Tools -- masterdata export json ^
  --input src/Game.Server/MasterData/masterdata.bytes ^
  --out-dir export/ ^
  --target server

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Exported to export/ directory.
) else (
    echo [ERROR] Export failed with error code %ERRORLEVEL%
)
echo.
pause
