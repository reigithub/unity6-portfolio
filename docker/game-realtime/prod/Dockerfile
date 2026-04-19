# docker/game-realtime/prod/Dockerfile
# Game.Realtime 本番用 Dockerfile

# ===== Build Stage =====
FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/Game.Shared/Game.Shared.csproj src/Game.Shared/
COPY src/Game.Server.Shared/Game.Server.Shared.csproj src/Game.Server.Shared/
COPY src/Game.Realtime/Game.Realtime.csproj src/Game.Realtime/
RUN dotnet restore src/Game.Realtime/Game.Realtime.csproj

COPY src/Game.Shared/ src/Game.Shared/
COPY src/Game.Server.Shared/ src/Game.Server.Shared/
COPY src/Game.Realtime/ src/Game.Realtime/

RUN dotnet publish src/Game.Realtime/Game.Realtime.csproj \
    -c Release \
    -o /app/publish

# ===== Runtime Stage =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0.15 AS runtime

RUN groupadd -r appgroup && useradd -r -g appgroup appuser

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "Game.Realtime.dll"]
