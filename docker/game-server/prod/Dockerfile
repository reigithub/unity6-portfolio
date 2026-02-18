# docker/game-server/prod/Dockerfile
# Google Cloud Run 用 Dockerfile

# ===== Build Stage =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 依存関係のみ先にコピー（キャッシュ活用）
COPY Directory.Packages.props ./
COPY src/Game.Shared/Game.Shared.csproj src/Game.Shared/
COPY src/Game.Server/Game.Server.csproj src/Game.Server/
RUN dotnet restore src/Game.Shared/Game.Shared.csproj
RUN dotnet restore src/Game.Server/Game.Server.csproj

# ソースコードをコピーしてビルド
COPY src/Game.Shared/ src/Game.Shared/
COPY src/Game.Server/ src/Game.Server/

# Release ビルド
RUN dotnet publish src/Game.Server/Game.Server.csproj \
    -c Release \
    -o /app/publish

# ===== Runtime Stage =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# 非rootユーザーで実行（セキュリティ）
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

WORKDIR /app
COPY --from=build /app/publish .

# 所有権を変更
RUN chown -R appuser:appgroup /app

# 非rootユーザーに切り替え
USER appuser

# Cloud Run は PORT 環境変数を設定する
# PORT 環境変数を使用してポートを動的に設定
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Cloud Run は独自のヘルスチェックを行うため HEALTHCHECK は不要
# アプリケーション側で /health エンドポイントを実装することを推奨

ENTRYPOINT ["dotnet", "Game.Server.dll"]
