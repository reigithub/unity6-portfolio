@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   MasterData Build (Client + Server)
echo ========================================
echo.

dotnet run --project src/Game.Tools -- masterdata build ^
  --tsv-dir masterdata/raw/ ^
  --proto-dir masterdata/proto/ ^
  --out-client src/Game.Client/Assets/MasterData/MasterDataBinary.bytes ^
  --out-server src/Game.Server/MasterData/masterdata.bytes

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Client and Server binaries generated.
) else (
    echo [ERROR] Build failed with error code %ERRORLEVEL%
)
echo.
pause
