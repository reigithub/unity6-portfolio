# Game.Server - ASP.NET Core 9.0 Multi-stage Build
#
# Build context: solution root (../../)
# Usage:
#   docker compose -f docker/game-server/docker-compose.yml build

# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore cache
COPY src/Game.Shared/Game.Shared.csproj src/Game.Shared/
COPY src/Game.Server/Game.Server.csproj src/Game.Server/
RUN dotnet restore src/Game.Server/Game.Server.csproj

# Copy source code
COPY src/Game.Shared/ src/Game.Shared/
COPY src/Game.Server/ src/Game.Server/

# Publish
RUN dotnet publish src/Game.Server/Game.Server.csproj -c Release -o /app/publish

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Game.Server.dll"]
