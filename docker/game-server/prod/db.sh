#!/bin/bash
# docker/game-server/prod/db.sh
# Cloud SQL データベース管理ツール（bash）
#
# 使用方法:
#   cd Unity6Portfolio/docker/game-server/prod
#   ./db.sh proxy        # Cloud SQL Auth Proxy を起動
#   ./db.sh migrate      # マイグレーション実行
#   ./db.sh seed         # シードデータ適用
#   ./db.sh status       # マイグレーション状態確認
#   ./db.sh reset        # データベースリセット（注意）
#   ./db.sh dump         # データダンプ

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

COMMAND="${1:-help}"
SCHEMA=""
FORCE=false
WITH_SEED=false
PROXY_PORT=5433

# 引数解析
shift || true
while [[ $# -gt 0 ]]; do
    case $1 in
        --schema) SCHEMA="$2"; shift 2 ;;
        --force) FORCE=true; shift ;;
        --with-seed) WITH_SEED=true; shift ;;
        --proxy-port) PROXY_PORT="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# .env ファイルを読み込み
ENV_FILE="$SCRIPT_DIR/.env"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    source <(grep -v '^#' "$ENV_FILE" | grep -v '^\s*$' | sed 's/\r$//')
    set +a
else
    echo "[ERROR] .env file not found."
    exit 1
fi

# 必須変数の確認
REQUIRED_VARS=("PROJECT_ID" "REGION" "INSTANCE_NAME" "DB_NAME" "DB_USER" "DB_PASSWORD")
for var in "${REQUIRED_VARS[@]}"; do
    if [[ -z "${!var}" ]]; then
        echo "[ERROR] Required variable $var is not set in .env"
        exit 1
    fi
done

# Cloud SQL 接続名
CONNECTION_NAME="$PROJECT_ID:$REGION:$INSTANCE_NAME"

# Proxy 経由の接続文字列
CONNECTION_STRING="Host=localhost;Port=$PROXY_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

show_help() {
    echo ""
    echo "Cloud SQL Database Management Tool"
    echo "==================================="
    echo ""
    echo "Usage: ./db.sh <command> [options]"
    echo ""
    echo "Commands:"
    echo "  proxy     Start Cloud SQL Auth Proxy (keep running in background)"
    echo "  migrate   Run pending database migrations"
    echo "  seed      Apply seed data from TSV files"
    echo "  status    Show current migration status"
    echo "  reset     Drop and recreate database (DANGEROUS)"
    echo "  dump      Dump database tables to TSV files"
    echo "  help      Show this help message"
    echo ""
    echo "Options:"
    echo "  --schema <name>     Target schema: master, user, or all (default: all)"
    echo "  --force             Skip confirmation prompts"
    echo "  --with-seed         Run seed after reset"
    echo "  --proxy-port <port> Proxy local port (default: 5433)"
    echo ""
    echo "Examples:"
    echo "  ./db.sh proxy                      # Start proxy (run first)"
    echo "  ./db.sh migrate                    # Run all migrations"
    echo "  ./db.sh migrate --schema master    # Run master schema only"
    echo "  ./db.sh seed                       # Apply seed data"
    echo "  ./db.sh reset --force --with-seed  # Reset and reseed"
    echo ""
}

start_proxy() {
    echo ""
    echo "===== Cloud SQL Auth Proxy ====="
    echo "Connection: $CONNECTION_NAME"
    echo "Local Port: $PROXY_PORT"
    echo "================================"
    echo ""

    # Proxy の存在確認
    PROXY_PATH="$SCRIPT_DIR/cloud-sql-proxy"
    if [[ ! -f "$PROXY_PATH" ]]; then
        echo "[INFO] Downloading Cloud SQL Auth Proxy..."
        if [[ "$(uname)" == "Darwin" ]]; then
            # macOS
            curl -o "$PROXY_PATH" https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.15.0/cloud-sql-proxy.darwin.amd64
        else
            # Linux
            curl -o "$PROXY_PATH" https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.15.0/cloud-sql-proxy.linux.amd64
        fi
        chmod +x "$PROXY_PATH"
        echo "[OK] Downloaded to $PROXY_PATH"
    fi

    echo "[INFO] Starting Cloud SQL Auth Proxy..."
    echo "[INFO] Press Ctrl+C to stop"
    echo ""

    "$PROXY_PATH" "$CONNECTION_NAME" --port="$PROXY_PORT"
}

invoke_game_tools() {
    cd "$PROJECT_ROOT"
    echo "[INFO] Running: dotnet run --project src/Game.Tools -- $*"
    dotnet run --project src/Game.Tools -- "$@"
}

test_proxy_running() {
    if ! nc -z localhost "$PROXY_PORT" 2>/dev/null; then
        echo ""
        echo "[ERROR] Cloud SQL Auth Proxy is not running on port $PROXY_PORT"
        echo "[INFO] Start the proxy first: ./db.sh proxy"
        echo ""
        exit 1
    fi
}

run_migrate() {
    test_proxy_running

    echo ""
    echo "===== Database Migration ====="
    echo "Database: $DB_NAME"
    echo "Schema:   ${SCHEMA:-all}"
    echo "=============================="
    echo ""

    ARGS=("migrate" "up" "--connection-string" "$CONNECTION_STRING")
    [[ -n "$SCHEMA" ]] && ARGS+=("--schema" "$SCHEMA")

    invoke_game_tools "${ARGS[@]}"

    echo ""
    echo "[OK] Migration completed"
}

run_seed() {
    test_proxy_running

    echo ""
    echo "===== Seed Data ====="
    echo "Database: $DB_NAME"
    echo "Source:   masterdata/raw/"
    echo "====================="
    echo ""

    ARGS=("seeddata" "seed" "--connection-string" "$CONNECTION_STRING")
    [[ -n "$SCHEMA" ]] && ARGS+=("--schema" "$SCHEMA")

    invoke_game_tools "${ARGS[@]}"

    echo ""
    echo "[OK] Seed completed"
}

run_status() {
    test_proxy_running

    echo ""
    echo "===== Migration Status ====="
    echo "Database: $DB_NAME"
    echo "============================"
    echo ""

    ARGS=("migrate" "status" "--connection-string" "$CONNECTION_STRING")
    [[ -n "$SCHEMA" ]] && ARGS+=("--schema" "$SCHEMA")

    invoke_game_tools "${ARGS[@]}"
}

run_reset() {
    test_proxy_running

    echo ""
    echo "===== Database Reset ====="
    echo "Database: $DB_NAME"
    echo "Schema:   ${SCHEMA:-all}"
    echo "WithSeed: $WITH_SEED"
    echo "=========================="
    echo ""

    if [[ "$FORCE" != "true" ]]; then
        echo "[WARNING] This will DROP ALL TABLES and recreate them!"
        read -p "Type 'yes' to confirm: " confirm
        if [[ "$confirm" != "yes" ]]; then
            echo "[INFO] Aborted"
            return
        fi
    fi

    ARGS=("migrate" "reset" "--connection-string" "$CONNECTION_STRING" "--force")
    [[ -n "$SCHEMA" ]] && ARGS+=("--schema" "$SCHEMA")
    [[ "$WITH_SEED" == "true" ]] && ARGS+=("--seed")
    ARGS+=("--version" "999999999999")

    invoke_game_tools "${ARGS[@]}"

    echo ""
    echo "[OK] Reset completed"
}

run_dump() {
    test_proxy_running

    echo ""
    echo "===== Database Dump ====="
    echo "Database: $DB_NAME"
    echo "Output:   masterdata/dump/"
    echo "========================="
    echo ""

    ARGS=("seeddata" "dump" "--connection-string" "$CONNECTION_STRING")
    [[ -n "$SCHEMA" ]] && ARGS+=("--schema" "$SCHEMA")

    invoke_game_tools "${ARGS[@]}"

    echo ""
    echo "[OK] Dump completed"
}

# メイン処理
case "$COMMAND" in
    proxy)   start_proxy ;;
    migrate) run_migrate ;;
    seed)    run_seed ;;
    status)  run_status ;;
    reset)   run_reset ;;
    dump)    run_dump ;;
    help)    show_help ;;
    *)       show_help ;;
esac
