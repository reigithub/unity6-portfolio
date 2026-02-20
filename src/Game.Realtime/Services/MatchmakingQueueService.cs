using Game.Server.Shared.Valkey;
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

    public Task EnqueuePlayerAsync(string userId, string gameMode)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await db.SortedSetAddAsync($"{QueueKeyPrefix}{gameMode}", userId, score);

            _logger.LogDebug("Player {UserId} enqueued for mode {GameMode}", userId, gameMode);
        },
        _logger,
        nameof(EnqueuePlayerAsync));
    }

    public Task DequeuePlayerAsync(string userId, string gameMode)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            await db.SortedSetRemoveAsync($"{QueueKeyPrefix}{gameMode}", userId);

            _logger.LogDebug("Player {UserId} dequeued from mode {GameMode}", userId, gameMode);
        },
        _logger,
        nameof(DequeuePlayerAsync));
    }

    public Task<int> GetQueueCountAsync(string gameMode)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            return (int)await db.SortedSetLengthAsync($"{QueueKeyPrefix}{gameMode}");
        },
        fallback: 0,
        _logger,
        nameof(GetQueueCountAsync));
    }

    public Task<string[]> DequeueTopPlayersAsync(string gameMode, int count)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
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
        },
        fallback: [],
        _logger,
        nameof(DequeueTopPlayersAsync));
    }
}
