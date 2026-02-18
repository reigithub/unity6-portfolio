using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのマッチメイキングキューサービス実装
/// </summary>
public class MatchmakingQueueService : IMatchmakingQueueService
{
    private const string QueueKeyPrefix = "matchmaking:queue:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MatchmakingQueueService> _logger;

    public MatchmakingQueueService(IConnectionMultiplexer redis, ILogger<MatchmakingQueueService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueuePlayerAsync(string userId, string gameMode)
    {
        try
        {
            var db = _redis.GetDatabase();
            var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await db.SortedSetAddAsync($"{QueueKeyPrefix}{gameMode}", userId, score);

            _logger.LogDebug("Player {UserId} enqueued for mode {GameMode}", userId, gameMode);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, could not enqueue player {UserId} for mode {GameMode}", userId, gameMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing player {UserId} for gameMode={GameMode}", userId, gameMode);
        }
    }

    public async Task DequeuePlayerAsync(string userId, string gameMode)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SortedSetRemoveAsync($"{QueueKeyPrefix}{gameMode}", userId);

            _logger.LogDebug("Player {UserId} dequeued from mode {GameMode}", userId, gameMode);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, could not dequeue player {UserId} from mode {GameMode}", userId, gameMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing player {UserId} for gameMode={GameMode}", userId, gameMode);
        }
    }

    public async Task<int> GetQueueCountAsync(string gameMode)
    {
        try
        {
            var db = _redis.GetDatabase();
            return (int)await db.SortedSetLengthAsync($"{QueueKeyPrefix}{gameMode}");
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, returning 0 for queue count for gameMode={GameMode}", gameMode);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue count for gameMode={GameMode}", gameMode);
            return 0;
        }
    }

    public async Task<string[]> DequeueTopPlayersAsync(string gameMode, int count)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{QueueKeyPrefix}{gameMode}";

            // ZPOPMIN で原子的に N 人取得
            var entries = await db.SortedSetPopAsync(key, count, Order.Ascending);

            var playerIds = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                playerIds[i] = entries[i].Element.ToString();
            }

            _logger.LogDebug(
                "Dequeued {Count} players from mode {GameMode}",
                playerIds.Length,
                gameMode);

            return playerIds;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, returning empty array for gameMode={GameMode}", gameMode);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing top players for gameMode={GameMode}", gameMode);
            return [];
        }
    }
}
