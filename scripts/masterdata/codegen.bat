@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   MasterData Codegen (Proto to C#)
echo ========================================
echo.

dotnet run --project src/Game.Tools -- masterdata codegen ^
  --proto-dir masterdata/proto/ ^
  --out-client src/Game.Client/Assets/Programs/Runtime/Shared/MasterData/ ^
  --out-server src/Game.Server/MasterData/

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] C# classes generated.
) else (
    echo [ERROR] Codegen failed with error code %ERRORLEVEL%
)
echo.
pause
