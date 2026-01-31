#!/bin/bash
# GitHub App インストールアクセストークン生成スクリプト
#
# 必要な環境変数:
#   GITHUB_APP_ID              - GitHub App ID
#   GITHUB_APP_INSTALLATION_ID - Installation ID
#   GITHUB_APP_PRIVATE_KEY     - 秘密鍵（PEM形式、改行は \n に置換）
#
# または:
#   GITHUB_APP_PRIVATE_KEY_BASE64 - 秘密鍵（Base64エンコード）

set -e

# 色付きログ出力
log_info() {
    echo -e "\033[0;32m[INFO]\033[0m $1" >&2
}

log_error() {
    echo -e "\033[0;31m[ERROR]\033[0m $1" >&2
}

# 環境変数の確認
APP_ID="${GITHUB_APP_ID}"
INSTALLATION_ID="${GITHUB_APP_INSTALLATION_ID}"

if [ -z "$APP_ID" ]; then
    log_error "GITHUB_APP_ID is required"
    exit 1
fi

if [ -z "$INSTALLATION_ID" ]; then
    log_error "GITHUB_APP_INSTALLATION_ID is required"
    exit 1
fi

# 秘密鍵の取得
PRIVATE_KEY_FILE=$(mktemp)
trap "rm -f $PRIVATE_KEY_FILE" EXIT

if [ -n "$GITHUB_APP_PRIVATE_KEY_BASE64" ]; then
    # Base64 デコード
    echo "$GITHUB_APP_PRIVATE_KEY_BASE64" | base64 -d > "$PRIVATE_KEY_FILE"
elif [ -n "$GITHUB_APP_PRIVATE_KEY" ]; then
    # \n を実際の改行に変換
    echo -e "$GITHUB_APP_PRIVATE_KEY" > "$PRIVATE_KEY_FILE"
elif [ -n "$GITHUB_APP_PRIVATE_KEY_FILE" ] && [ -f "$GITHUB_APP_PRIVATE_KEY_FILE" ]; then
    # ファイルから読み込み
    cp "$GITHUB_APP_PRIVATE_KEY_FILE" "$PRIVATE_KEY_FILE"
else
    log_error "Private key is required. Set one of:"
    log_error "  - GITHUB_APP_PRIVATE_KEY (PEM string with \\n)"
    log_error "  - GITHUB_APP_PRIVATE_KEY_BASE64 (Base64 encoded)"
    log_error "  - GITHUB_APP_PRIVATE_KEY_FILE (path to .pem file)"
    exit 1
fi

# 秘密鍵のパーミッションを設定
chmod 600 "$PRIVATE_KEY_FILE"

log_info "Generating JWT for App ID: $APP_ID"

# JWT 生成
# 時刻ずれ対策: iat を過去に、exp を控えめに設定
now=$(date +%s)
iat=$((now - 120))     # 2分前から有効（時刻ずれ対策）
exp=$((now + 300))     # 5分後に失効（最大10分だが余裕を持たせる）

# Header (Base64URL)
header=$(echo -n '{"alg":"RS256","typ":"JWT"}' | openssl base64 -e | tr -d '=' | tr '/+' '_-' | tr -d '\n')

# Payload (Base64URL)
payload=$(echo -n "{\"iat\":${iat},\"exp\":${exp},\"iss\":\"${APP_ID}\"}" | openssl base64 -e | tr -d '=' | tr '/+' '_-' | tr -d '\n')

# Signature
signature=$(echo -n "${header}.${payload}" | openssl dgst -sha256 -sign "$PRIVATE_KEY_FILE" | openssl base64 -e | tr -d '=' | tr '/+' '_-' | tr -d '\n')

# JWT
jwt="${header}.${payload}.${signature}"

log_info "Requesting installation access token for Installation ID: $INSTALLATION_ID"

# インストールアクセストークン取得
response=$(curl --http1.1 -s -X POST \
    -H "Authorization: Bearer ${jwt}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "https://api.github.com/app/installations/${INSTALLATION_ID}/access_tokens")

# エラーチェック
if echo "$response" | jq -e '.message' > /dev/null 2>&1; then
    error_message=$(echo "$response" | jq -r '.message')
    log_error "GitHub API error: $error_message"
    exit 1
fi

# トークン抽出
token=$(echo "$response" | jq -r '.token')
expires_at=$(echo "$response" | jq -r '.expires_at')

if [ "$token" == "null" ] || [ -z "$token" ]; then
    log_error "Failed to get installation access token"
    log_error "Response: $response"
    exit 1
fi

log_info "Token obtained successfully (expires: $expires_at)"

# トークンを標準出力に出力
echo "$token"
