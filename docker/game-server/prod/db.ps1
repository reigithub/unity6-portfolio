# docker/game-server/prod/db.ps1
# Cloud SQL データベース管理ツール（PowerShell）
#
# 使用方法:
#   cd E:\UnityProjects\Unity6Portfolio\docker\game-server\prod
#   .\db.ps1 proxy        # Cloud SQL Auth Proxy を起動
#   .\db.ps1 migrate      # マイグレーション実行
#   .\db.ps1 seed         # シードデータ適用
#   .\db.ps1 status       # マイグレーション状態確認
#   .\db.ps1 reset        # データベースリセット（注意）
#   .\db.ps1 dump         # データダンプ

param(
    [Parameter(Position=0)]
    [ValidateSet("proxy", "migrate", "seed", "status", "reset", "dump", "help")]
    [string]$Command = "help",

    [string]$Schema = "",           # master, user, all
    [switch]$Force,                 # reset 時の確認スキップ
    [switch]$WithSeed,              # reset 後にシード実行
    [int]$ProxyPort = 5433          # Proxy のローカルポート
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\..\..\..\"

# .env ファイルを読み込み
$EnvFile = "$ScriptDir\.env"
if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            if ($value -ne '') {
                Set-Item -Path "Env:$name" -Value $value
            }
        }
    }
} else {
    Write-Host "[ERROR] .env file not found." -ForegroundColor Red
    exit 1
}

# 必須変数の確認
$RequiredVars = @("PROJECT_ID", "REGION", "INSTANCE_NAME", "DB_NAME", "DB_USER", "DB_PASSWORD")
foreach ($var in $RequiredVars) {
    if (-not (Get-Item -Path "Env:$var" -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Required variable $var is not set in .env" -ForegroundColor Red
        exit 1
    }
}

# Cloud SQL 接続名
$CONNECTION_NAME = "$env:PROJECT_ID`:$env:REGION`:$env:INSTANCE_NAME"

# Proxy 経由の接続文字列
$CONNECTION_STRING = "Host=localhost;Port=$ProxyPort;Database=$env:DB_NAME;Username=$env:DB_USER;Password=$env:DB_PASSWORD"

function Show-Help {
    Write-Host ""
    Write-Host "Cloud SQL Database Management Tool" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\db.ps1 <command> [options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  proxy     Start Cloud SQL Auth Proxy (keep running in background)"
    Write-Host "  migrate   Run pending database migrations"
    Write-Host "  seed      Apply seed data from TSV files"
    Write-Host "  status    Show current migration status"
    Write-Host "  reset     Drop and recreate database (DANGEROUS)"
    Write-Host "  dump      Dump database tables to TSV files"
    Write-Host "  help      Show this help message"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Schema <name>    Target schema: master, user, or all (default: all)"
    Write-Host "  -Force            Skip confirmation prompts"
    Write-Host "  -WithSeed         Run seed after reset"
    Write-Host "  -ProxyPort <port> Proxy local port (default: 5433)"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\db.ps1 proxy                    # Start proxy (run first)"
    Write-Host "  .\db.ps1 migrate                  # Run all migrations"
    Write-Host "  .\db.ps1 migrate -Schema master   # Run master schema only"
    Write-Host "  .\db.ps1 seed                     # Apply seed data"
    Write-Host "  .\db.ps1 reset -Force -WithSeed   # Reset and reseed"
    Write-Host ""
}

function Start-Proxy {
    Write-Host ""
    Write-Host "===== Cloud SQL Auth Proxy =====" -ForegroundColor Cyan
    Write-Host "Connection: $CONNECTION_NAME"
    Write-Host "Local Port: $ProxyPort"
    Write-Host "================================" -ForegroundColor Cyan
    Write-Host ""

    # Proxy の存在確認
    $ProxyPath = "$ScriptDir\cloud-sql-proxy.exe"
    if (-not (Test-Path $ProxyPath)) {
        Write-Host "[INFO] Downloading Cloud SQL Auth Proxy..." -ForegroundColor Yellow
        $ProxyUrl = "https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.15.0/cloud-sql-proxy.x64.exe"
        Invoke-WebRequest -Uri $ProxyUrl -OutFile $ProxyPath
        Write-Host "[OK] Downloaded to $ProxyPath" -ForegroundColor Green
    }

    Write-Host "[INFO] Starting Cloud SQL Auth Proxy..." -ForegroundColor Yellow
    Write-Host "[INFO] Press Ctrl+C to stop" -ForegroundColor Yellow
    Write-Host ""

    & $ProxyPath $CONNECTION_NAME --port=$ProxyPort
}

function Invoke-GameTools {
    param([string[]]$Arguments)

    Push-Location $ProjectRoot
    try {
        Write-Host "[INFO] Running: dotnet run --project src/Game.Tools -- $($Arguments -join ' ')" -ForegroundColor Gray
        & dotnet run --project src/Game.Tools -- @Arguments
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Command failed with exit code $LASTEXITCODE" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    } finally {
        Pop-Location
    }
}

function Test-ProxyRunning {
    $tcpConnection = Get-NetTCPConnection -LocalPort $ProxyPort -ErrorAction SilentlyContinue
    if (-not $tcpConnection) {
        Write-Host ""
        Write-Host "[ERROR] Cloud SQL Auth Proxy is not running on port $ProxyPort" -ForegroundColor Red
        Write-Host "[INFO] Start the proxy first: .\db.ps1 proxy" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

function Run-Migrate {
    Test-ProxyRunning

    Write-Host ""
    Write-Host "===== Database Migration =====" -ForegroundColor Cyan
    Write-Host "Database: $env:DB_NAME"
    Write-Host "Schema:   $(if ($Schema) { $Schema } else { 'all' })"
    Write-Host "==============================" -ForegroundColor Cyan
    Write-Host ""

    $args = @("migrate", "up", "--connection-string", $CONNECTION_STRING)
    if ($Schema) { $args += "--schema"; $args += $Schema }

    Invoke-GameTools $args

    Write-Host ""
    Write-Host "[OK] Migration completed" -ForegroundColor Green
}

function Run-Seed {
    Test-ProxyRunning

    Write-Host ""
    Write-Host "===== Seed Data =====" -ForegroundColor Cyan
    Write-Host "Database: $env:DB_NAME"
    Write-Host "Source:   masterdata/raw/"
    Write-Host "=====================" -ForegroundColor Cyan
    Write-Host ""

    $args = @("seeddata", "seed", "--connection-string", $CONNECTION_STRING)
    if ($Schema) { $args += "--schema"; $args += $Schema }

    Invoke-GameTools $args

    Write-Host ""
    Write-Host "[OK] Seed completed" -ForegroundColor Green
}

function Run-Status {
    Test-ProxyRunning

    Write-Host ""
    Write-Host "===== Migration Status =====" -ForegroundColor Cyan
    Write-Host "Database: $env:DB_NAME"
    Write-Host "============================" -ForegroundColor Cyan
    Write-Host ""

    $args = @("migrate", "status", "--connection-string", $CONNECTION_STRING)
    if ($Schema) { $args += "--schema"; $args += $Schema }

    Invoke-GameTools $args
}

function Run-Reset {
    Test-ProxyRunning

    Write-Host ""
    Write-Host "===== Database Reset =====" -ForegroundColor Red
    Write-Host "Database: $env:DB_NAME"
    Write-Host "Schema:   $(if ($Schema) { $Schema } else { 'all' })"
    Write-Host "WithSeed: $WithSeed"
    Write-Host "==========================" -ForegroundColor Red
    Write-Host ""

    if (-not $Force) {
        Write-Host "[WARNING] This will DROP ALL TABLES and recreate them!" -ForegroundColor Red
        $confirm = Read-Host "Type 'yes' to confirm"
        if ($confirm -ne "yes") {
            Write-Host "[INFO] Aborted" -ForegroundColor Yellow
            return
        }
    }

    $args = @("migrate", "reset", "--connection-string", $CONNECTION_STRING, "--force")
    if ($Schema) { $args += "--schema"; $args += $Schema }
    if ($WithSeed) { $args += "--seed" }
    # version を指定して最新までマイグレーション
    $args += "--version"
    $args += "999999999999"

    Invoke-GameTools $args

    Write-Host ""
    Write-Host "[OK] Reset completed" -ForegroundColor Green
}

function Run-Dump {
    Test-ProxyRunning

    Write-Host ""
    Write-Host "===== Database Dump =====" -ForegroundColor Cyan
    Write-Host "Database: $env:DB_NAME"
    Write-Host "Output:   masterdata/dump/"
    Write-Host "=========================" -ForegroundColor Cyan
    Write-Host ""

    $args = @("seeddata", "dump", "--connection-string", $CONNECTION_STRING)
    if ($Schema) { $args += "--schema"; $args += $Schema }

    Invoke-GameTools $args

    Write-Host ""
    Write-Host "[OK] Dump completed" -ForegroundColor Green
}

# メイン処理
switch ($Command) {
    "proxy"   { Start-Proxy }
    "migrate" { Run-Migrate }
    "seed"    { Run-Seed }
    "status"  { Run-Status }
    "reset"   { Run-Reset }
    "dump"    { Run-Dump }
    "help"    { Show-Help }
    default   { Show-Help }
}
