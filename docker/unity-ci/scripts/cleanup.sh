#!/bin/bash
# Cleanup script for removing runner registration
# Use this to manually remove a runner from GitHub

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

if [ -z "$GITHUB_REPOSITORY" ] || [ -z "$GITHUB_TOKEN" ]; then
    log_error "GITHUB_REPOSITORY and GITHUB_TOKEN environment variables are required"
    log_error "Usage: GITHUB_REPOSITORY=owner/repo GITHUB_TOKEN=xxx ./cleanup.sh"
    exit 1
fi

log_info "Getting removal token..."
REMOVE_TOKEN=$(curl --http1.1 -s -X POST \
    -H "Authorization: token ${GITHUB_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runners/registration-token" \
    | jq -r .token)

if [ "$REMOVE_TOKEN" == "null" ] || [ -z "$REMOVE_TOKEN" ]; then
    log_error "Failed to get removal token"
    exit 1
fi

log_info "Removing runner..."
./config.sh remove --token "${REMOVE_TOKEN}"

log_info "Runner removed successfully"
