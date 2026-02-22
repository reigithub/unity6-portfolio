using System.Net.Security;
using Google.Apis.Auth.OAuth2;
using Medallion.Threading;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace Game.Server.Shared.Extensions;

/// <summary>
/// Valkey/Redis 接続 + 分散ロック登録の共通拡張メソッド
/// </summary>
public static class ValkeyServiceExtensions
{
    private static readonly TimeSpan _tokenRefreshInterval = TimeSpan.FromMinutes(4);

    /// <summary>
    /// IConnectionMultiplexer を DI に登録（接続文字列は ConnectionStrings:Valkey から取得）
    /// ssl=true が含まれる場合、GCP IAM 認証モードで接続する
    /// </summary>
    public static IServiceCollection AddValkeyConnection(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Valkey")
            ?? throw new InvalidOperationException("ConnectionStrings:Valkey is not configured.");

        var options = ConfigurationOptions.Parse(connectionString);
        if (options.Ssl)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
                return ConnectWithAuthAsync(options, logger).GetAwaiter().GetResult();
            });
        }
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
                try
                {
                    var multiplexer = ConnectionMultiplexer.Connect(options);
                    logger.LogInformation("Connected to Valkey/Redis");
                    return multiplexer;
                }
                catch (RedisConnectionException ex)
                {
                    logger.LogWarning(ex, "Failed to connect to Valkey/Redis. Retrying...");
                    return ConnectionMultiplexer.Connect(options);
                }
            });
        }

        return services;
    }

    /// <summary>
    /// Redis ベースの IDistributedLockProvider を DI に登録
    /// </summary>
    public static IServiceCollection AddDistributedLock(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisDistributedSynchronizationProvider(redis.GetDatabase(), options =>
            {
                options.Expiry(TimeSpan.FromSeconds(10));
                options.ExtensionCadence(TimeSpan.FromSeconds(3));
                options.BusyWaitSleepTime(
                    TimeSpan.FromMilliseconds(10),
                    TimeSpan.FromMilliseconds(200));
            });
        });

        return services;
    }

    private static async Task<IConnectionMultiplexer> ConnectWithAuthAsync(ConfigurationOptions options, ILogger logger)
    {
        // GCP IAM 認証モードで接続する
        var credential = await GoogleCredential.GetApplicationDefaultAsync();
        var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

        options.User = "default";
        options.Password = token;

        // GCP Memorystore の内部 CA 証明書を信頼する
        options.CertificateValidation += (_, _, _, errors) =>
            errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors;

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
        logger.LogInformation("Connected to Valkey/Redis with IAM authentication");

        // トークンリフレッシュタイマー（4分間隔、トークン有効期限は1時間）
        _ = new Timer(
            delegate { _ = RefreshTokenAsync(credential, multiplexer, logger); },
            null,
            _tokenRefreshInterval,
            _tokenRefreshInterval);

        multiplexer.ConnectionFailed += (_, args) =>
            logger.LogWarning("Valkey connection failed: {FailureType}", args.FailureType);
        multiplexer.ConnectionRestored += (_, _) =>
            logger.LogInformation("Valkey connection restored");

        return multiplexer;
    }

    private static async Task RefreshTokenAsync(GoogleCredential credential, IConnectionMultiplexer multiplexer, ILogger logger)
    {
        try
        {
            var newToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            foreach (var server in multiplexer.GetServers())
            {
                await server.ExecuteAsync("AUTH", "default", newToken);
            }

            logger.LogDebug("Valkey IAM token refreshed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh Valkey IAM token");
        }
    }
}
