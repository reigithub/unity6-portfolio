using StackExchange.Redis;

namespace Game.Server.Shared.Valkey;

/// <summary>
/// Valkey (Redis互換) 操作の2段キャッチパターンを集約するヘルパー。
/// RedisException / RedisTimeoutException → Warning、それ以外 → Error でログし、フォールバック値を返す。
/// RedisTimeoutException は TimeoutException を継承しており RedisException の派生ではないため、
/// when フィルターで両方を Warning レベルに統合する。
/// </summary>
public static class ValkeyExecutor
{
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        T fallback,
        ILogger logger,
        string operationName)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Valkey error in {Operation}, returning fallback", operationName);
            return fallback;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Operation}", operationName);
            return fallback;
        }
    }

    public static async Task ExecuteAsync(
        Func<Task> operation,
        ILogger logger,
        string operationName)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Valkey error in {Operation}", operationName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Operation}", operationName);
        }
    }
}
