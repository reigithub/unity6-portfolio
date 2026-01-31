# Unity 6 GitHub Actions Self-hosted Runner
# Based on GameCI Unity Docker Images
#
# ============================================
# ビルドプラットフォーム選択ガイド
# ============================================
#
# GameCI イメージタグ形式:
#   unityci/editor:ubuntu-{UNITY_VERSION}-{MODULE}-{IMAGE_VERSION}
#
# 利用可能なモジュール:
#   - base          : Unity Editor のみ（ビルドモジュールなし）
#   - windows-mono  : Windows ビルド用 (Mono) - デフォルト
#   - linux-il2cpp  : Linux ビルド用 (IL2CPP)
#   - mac-mono      : macOS ビルド用 (Mono)
#   - webgl         : WebGL ビルド用
#   - android       : Android ビルド用
#   - ios           : iOS ビルド用（Xcode プロジェクト出力のみ）
#
# 設定方法: .env ファイルで UNITY_MODULE を変更

# ビルド引数でプラットフォームを指定可能
ARG UNITY_VERSION=6000.3.2f1
ARG UNITY_MODULE=windows-mono
ARG IMAGE_VERSION=3

FROM unityci/editor:ubuntu-${UNITY_VERSION}-${UNITY_MODULE}-${IMAGE_VERSION}

# Build arguments
ARG UNITY_VERSION
ARG UNITY_MODULE
ARG RUNNER_VERSION=2.322.0
ARG DEBIAN_FRONTEND=noninteractive

# Labels
LABEL maintainer="your-email@example.com"
LABEL description="Unity 6 GitHub Actions Self-hosted Runner"
LABEL unity.version="${UNITY_VERSION}"
LABEL unity.module="${UNITY_MODULE}"

# Install dependencies (including .NET Core 6.0 requirements for GitHub Actions Runner)
RUN apt-get update && apt-get install -y \
    curl \
    jq \
    git \
    git-lfs \
    libicu-dev \
    libssl-dev \
    ca-certificates \
    sudo \
    xvfb \
    libgtk-3-0 \
    libglu1-mesa \
    openssl \
    dos2unix \
    libstdc++6 \
    libkrb5-3 \
    zlib1g \
    && rm -rf /var/lib/apt/lists/*

# Create runner user
RUN useradd -m -s /bin/bash runner \
    && echo "runner ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers

# Setup GitHub Actions Runner
WORKDIR /home/runner/actions-runner

RUN curl -o actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz -L \
    https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz \
    && tar xzf actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz \
    && rm actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz \
    && ./bin/installdependencies.sh \
    && chown -R runner:runner /home/runner

# Copy scripts and convert line endings (CRLF -> LF)
COPY scripts/entrypoint.sh /entrypoint.sh
COPY scripts/github-app-token.sh /github-app-token.sh
COPY scripts/cleanup.sh /cleanup.sh
RUN dos2unix /entrypoint.sh /github-app-token.sh /cleanup.sh && chmod +x /entrypoint.sh /github-app-token.sh /cleanup.sh

# Unity cache and license directories
# Also create _work directory for runner updates
RUN mkdir -p /unity-cache \
    && mkdir -p /home/runner/.local/share/unity3d/Unity \
    && mkdir -p /home/runner/.unity-library-cache \
    && mkdir -p /home/runner/actions-runner/_work \
    && chown -R runner:runner /unity-cache /home/runner/.local /home/runner/.unity-library-cache /home/runner/actions-runner

# X11 ソケットディレクトリのセットアップ（xvfb-run 用）
RUN mkdir -p /tmp/.X11-unix \
    && chmod 1777 /tmp/.X11-unix

# Switch to runner user
USER runner
WORKDIR /home/runner/actions-runner

# Environment variables
ENV RUNNER_ALLOW_RUNASROOT=false
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["/entrypoint.sh"]
