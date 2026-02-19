using Medallion.Threading;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace Game.Server.Shared.Extensions;

/// <summary>
/// Valkey/Redis 接続 + 分散ロック登録の共通拡張メソッド
/// </summary>
public static class ValkeyServiceExtensions
{
    /// <summary>
    /// IConnectionMultiplexer を DI に登録（接続文字列は ConnectionStrings:Valkey から取得）
    /// </summary>
    public static IServiceCollection AddValkeyConnection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Valkey")
            ?? "localhost:6379,abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(connectionString);
                logger.LogInformation("Connected to Valkey/Redis");
                return multiplexer;
            }
            catch (RedisConnectionException ex)
            {
                logger.LogWarning(ex,
                    "Failed to connect to Valkey/Redis. Retrying with options...");
                return ConnectionMultiplexer.Connect(
                    ConfigurationOptions.Parse(connectionString));
            }
        });

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
}
