@echo off
chcp 65001 > nul
cd /d "%~dp0..\.."

echo ========================================
echo   Diff TSV Directories
echo ========================================
echo.

set SOURCE_DIR=%1
set TARGET_DIR=%2

if "%SOURCE_DIR%"=="" set SOURCE_DIR=masterdata/raw/
if "%TARGET_DIR%"=="" set TARGET_DIR=masterdata/dump/

echo Source: %SOURCE_DIR%
echo Target: %TARGET_DIR%
echo.

dotnet run --project src/Game.Tools -- seeddata diff ^
  --source-dir %SOURCE_DIR% ^
  --target-dir %TARGET_DIR%

echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] All files match.
) else (
    echo [WARNING] Differences found.
)
echo.
pause
