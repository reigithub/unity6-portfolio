# Migration Job Dockerfile
# Cloud Run Job でマイグレーションを実行するためのイメージ

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# プロジェクトファイルをコピーして依存関係を復元
COPY src/Game.Shared/Game.Shared.csproj src/Game.Shared/
COPY src/Game.Tools/Game.Tools.csproj src/Game.Tools/
RUN dotnet restore src/Game.Tools/Game.Tools.csproj

# ソースコードをコピー
COPY src/Game.Shared/ src/Game.Shared/
COPY src/Game.Tools/ src/Game.Tools/

# マスターデータとスクリプトをコピー
COPY masterdata/ masterdata/
COPY scripts/ scripts/

# ビルド
RUN dotnet build src/Game.Tools/Game.Tools.csproj -c Release

# エントリーポイント
# 使用例:
#   migrate up
#   migrate down --steps 1
#   migrate status
#   seeddata seed --tsv-dir masterdata/raw/
ENTRYPOINT ["dotnet", "run", "--project", "src/Game.Tools", "--no-build", "-c", "Release", "--"]
CMD ["migrate", "status"]
