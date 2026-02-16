using Game.Realtime.Services;

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
        // Match Session Token Service (Dedicated Server 接続認証用)
        services.AddSingleton<IMatchSessionTokenService, MatchSessionTokenService>();

        // Matchmaking Service
        services.AddSingleton<IMatchmakingService, MatchmakingService>();

        return services;
    }

    /// <summary>
    /// Realtime サーバーのエンドポイントをマッピング
    /// </summary>
    public static WebApplication MapRealtimeEndpoints(this WebApplication app)
    {
        // Health check endpoint (gRPC Health Checking Protocol)
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Game.Realtime" }));

        return app;
    }
}
