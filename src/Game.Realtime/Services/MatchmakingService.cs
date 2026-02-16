using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのマッチメイキングサービス実装
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private const string QueueKeyPrefix = "matchmaking:queue:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(IConnectionMultiplexer redis, ILogger<MatchmakingService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueuePlayerAsync(string userId, string gameMode)
    {
        var db = _redis.GetDatabase();
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await db.SortedSetAddAsync($"{QueueKeyPrefix}{gameMode}", userId, score);

        _logger.LogDebug("Player {UserId} enqueued for mode {GameMode}", userId, gameMode);
    }

    public async Task DequeuePlayerAsync(string userId, string gameMode)
    {
        var db = _redis.GetDatabase();
        await db.SortedSetRemoveAsync($"{QueueKeyPrefix}{gameMode}", userId);

        _logger.LogDebug("Player {UserId} dequeued from mode {GameMode}", userId, gameMode);
    }

    public async Task<int> GetQueueCountAsync(string gameMode)
    {
        var db = _redis.GetDatabase();
        return (int)await db.SortedSetLengthAsync($"{QueueKeyPrefix}{gameMode}");
    }
}
