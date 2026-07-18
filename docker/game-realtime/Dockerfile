# Game.Realtime - MagicOnion gRPC Server Multi-stage Build
# Build context: solution root (../../)
# Usage: docker compose -f docker/game-server/docker-compose.yml build game-realtime-app

# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0.316 AS build
WORKDIR /src

# Copy project files for restore cache
COPY Directory.Packages.props ./
COPY src/Game.Shared/Game.Shared.csproj src/Game.Shared/
COPY src/Game.Server.Shared/Game.Server.Shared.csproj src/Game.Server.Shared/
COPY src/Game.Realtime/Game.Realtime.csproj src/Game.Realtime/
RUN dotnet restore src/Game.Realtime/Game.Realtime.csproj

# Copy source code
COPY src/Game.Shared/ src/Game.Shared/
COPY src/Game.Server.Shared/ src/Game.Server.Shared/
COPY src/Game.Realtime/ src/Game.Realtime/

# Publish
RUN dotnet publish src/Game.Realtime/Game.Realtime.csproj -c Release -o /app/publish

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0.18 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appgroup && useradd -r -g appgroup appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

EXPOSE 5001

ENTRYPOINT ["dotnet", "Game.Realtime.dll"]
