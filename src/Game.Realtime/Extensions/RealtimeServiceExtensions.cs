using Game.Realtime.Services;
using Game.Realtime.Validation;
using Game.Server.Shared.Extensions;
using Game.Server.Shared.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Game.Realtime.Extensions;

/// <summary>
/// Realtime サーバー用 DI 登録 + エンドポイントマッピング拡張メソッド
/// </summary>
public static class RealtimeServiceExtensions
{
    /// <summary>
    /// Realtime サーバーのアプリケーションサービスを DI に登録
    /// </summary>
    public static IServiceCollection AddRealtimeServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Distributed Lock Provider (レースコンディション防止)
        services.AddDistributedLock();

        // Game.Server API クライアント（サービス間 HTTP 通信）
        services.AddHttpClient();
        services.AddSingleton<IUnityServerAuthApiClient, UnityServerAuthApiClient>();

        // Matchmaking Queue Service
        services.AddSingleton<IMatchmakingQueueService, MatchmakingQueueService>();

        // Lobby Data Service
        services.AddSingleton<ILobbyDataService, LobbyDataService>();

        // Matchmaking Configuration
        services.Configure<MatchmakingConfiguration>(
            configuration.GetSection("Matchmaking"));

        // Unity Dedicated Server Configuration
        services.Configure<UnityServerConfiguration>(
            configuration.GetSection("UnityServer"));

        // Game.Server 接続設定
        services.AddOptions<GameServerSettings>()
            .Bind(configuration.GetSection("GameServer"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Matchmaking Background Processor
        services.AddHostedService<MatchmakingProcessor>();

        // Validators
        services.AddSingleton<IMatchmakingValidator, MatchmakingValidator>();
        services.AddSingleton<ILobbyValidator, LobbyValidator>();

        // Health Checks
        services.AddHealthChecks()
            .AddCheck<ValkeyHealthCheck>("valkey", tags: new[] { "ready" });

        return services;
    }

    /// <summary>
    /// Realtime サーバーのエンドポイントをマッピング
    /// </summary>
    public static WebApplication MapRealtimeEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        });

        return app;
    }
}
