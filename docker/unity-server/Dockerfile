# Unity Dedicated Server (Headless Linux)
#
# ビルドコンテキスト: Linux Server のビルド出力ディレクトリ
# 使用方法:
#   docker build -t game-unity-server \
#     -f docker/unity-server/Dockerfile \
#     src/Game.Client/Builds/Server/Linux/
#
# ビルド済みバイナリが必要:
#   Unity Editor → Build > Server > Linux Dedicated Server

FROM ubuntu:22.04

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libstdc++6 \
    libgcc-s1 \
    libc6 \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r gameserver && useradd -r -g gameserver -d /app gameserver

WORKDIR /app

COPY . .

# ビルド時の不要ファイルを除去 + 実行権限付与
RUN rm -rf *_BackUpThisFolder_ButDontShipItWithYourGame \
           *_BurstDebugInformation_DoNotShip \
    && chmod +x Unity6GameServer \
    && chown -R gameserver:gameserver /app

USER gameserver

# Mirror KCP (UDP) + TCP Health Probe
EXPOSE 7777/udp
EXPOSE 7778/tcp

# TCP ヘルスチェック（bash の /dev/tcp を使用、追加パッケージ不要）
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=3 \
    CMD bash -c 'echo > /dev/tcp/localhost/7778' || exit 1

ENTRYPOINT ["./Unity6GameServer", "-batchmode", "-nographics"]
CMD ["--port", "7777", "--health-port", "7778", "--players", "4"]
