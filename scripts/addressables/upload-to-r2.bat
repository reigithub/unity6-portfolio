@echo off
REM Cloudflare R2 アップロードスクリプト
REM Usage: upload-to-r2.bat [Platform] [--dry-run]

setlocal

set SCRIPT_DIR=%~dp0

REM 引数を処理
set PLATFORM=
set DRY_RUN=

:parse_args
if "%~1"=="" goto run
if /i "%~1"=="--dry-run" (
    set DRY_RUN=-DryRun
    shift
    goto parse_args
)
if /i "%~1"=="-d" (
    set DRY_RUN=-DryRun
    shift
    goto parse_args
)
set PLATFORM=-Platform %~1
shift
goto parse_args

:run
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%upload-to-r2.ps1" %PLATFORM% %DRY_RUN%

endlocal
