using Game.Server.Shared.Valkey;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのマッチメイキングキューサービス実装
/// stageId 別キュー + "any" キュー + matchSize 対応
/// </summary>
public class MatchmakingQueueService : IMatchmakingQueueService
{
    private const string QueueKeyPrefix = "matchmaking:queue:";
    private const string StagesKeyPrefix = "matchmaking:stages:";
    private const string PlayerKeyPrefix = "matchmaking:player:";
    private const string AnyStageKey = "any";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MatchmakingQueueService> _logger;

    public MatchmakingQueueService(IConnectionMultiplexer redis, ILogger<MatchmakingQueueService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public Task EnqueuePlayerAsync(string userId, string gameMode, int stageId, int matchSize)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var stageKey = stageId > 0 ? stageId.ToString() : AnyStageKey;
            var queueKey = $"{QueueKeyPrefix}{gameMode}:{stageKey}";
            var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var batch = db.CreateBatch();
            var t1 = batch.SortedSetAddAsync(queueKey, userId, score);
            var t2 = batch.SetAddAsync($"{StagesKeyPrefix}{gameMode}", stageKey);
            var t3 = batch.HashSetAsync($"{PlayerKeyPrefix}{userId}", "matchSize", matchSize);
            batch.Execute();

            await Task.WhenAll(t1, t2, t3);

            _logger.LogDebug(
                "Player {UserId} enqueued for mode {GameMode}, stage {StageKey}, matchSize {MatchSize}",
                userId, gameMode, stageKey, matchSize);
        },
        _logger,
        nameof(EnqueuePlayerAsync));
    }

    public Task DequeuePlayerAsync(string userId, string gameMode, int stageId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var stageKey = stageId > 0 ? stageId.ToString() : AnyStageKey;
            var queueKey = $"{QueueKeyPrefix}{gameMode}:{stageKey}";

            var batch = db.CreateBatch();
            var t1 = batch.SortedSetRemoveAsync(queueKey, userId);
            var t2 = batch.KeyDeleteAsync($"{PlayerKeyPrefix}{userId}");
            batch.Execute();

            await Task.WhenAll(t1, t2);

            _logger.LogDebug("Player {UserId} dequeued from mode {GameMode}, stage {StageKey}", userId, gameMode, stageKey);
        },
        _logger,
        nameof(DequeuePlayerAsync));
    }

    public Task<int> GetQueueCountAsync(string gameMode, int stageId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var stageKey = stageId > 0 ? stageId.ToString() : AnyStageKey;
            return checked((int)await db.SortedSetLengthAsync($"{QueueKeyPrefix}{gameMode}:{stageKey}"));
        },
        fallback: 0,
        _logger,
        nameof(GetQueueCountAsync));
    }

    public Task<string[]> DequeueTopPlayersAsync(string gameMode, int stageId, int count)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var stageKey = stageId > 0 ? stageId.ToString() : AnyStageKey;
            var key = $"{QueueKeyPrefix}{gameMode}:{stageKey}";

            // ZPOPMIN で原子的に N 人取得
            var entries = await db.SortedSetPopAsync(key, count, Order.Ascending);

            var playerIds = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                playerIds[i] = entries[i].Element.ToString();
            }

            _logger.LogDebug(
                "Dequeued {Count} players from mode {GameMode}, stage {StageKey}",
                playerIds.Length, gameMode, stageKey);

            return playerIds;
        },
        fallback: [],
        _logger,
        nameof(DequeueTopPlayersAsync));
    }

    public Task<string[]> GetActiveStageKeysAsync(string gameMode)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync($"{StagesKeyPrefix}{gameMode}");
            var result = new string[members.Length];
            for (var i = 0; i < members.Length; i++)
            {
                result[i] = members[i].ToString();
            }
            return result;
        },
        fallback: [],
        _logger,
        nameof(GetActiveStageKeysAsync));
    }

    public Task<int> GetPlayerMatchSizeAsync(string userId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            var value = await db.HashGetAsync($"{PlayerKeyPrefix}{userId}", "matchSize");
            return value.HasValue ? (int)value : 2;
        },
        fallback: 2,
        _logger,
        nameof(GetPlayerMatchSizeAsync));
    }

    public Task CleanupPlayerAsync(string userId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"{PlayerKeyPrefix}{userId}");
        },
        _logger,
        nameof(CleanupPlayerAsync));
    }
}
