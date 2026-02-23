using Game.Library.Shared.Dto;
using Game.Server.Configuration;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Game.Server.Shared.Valkey;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Server.Services;

/// <summary>
/// Valkey (Redis互換) を使用したランキングキャッシュサービス
/// Sorted Set でランキングを管理し、高速な順位取得を実現
/// </summary>
public class SurvivorRankingCacheService : ISurvivorRankingCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<SurvivorRankingCacheService> _logger;
    private readonly TimeSpan _defaultExpiry;

    // キー形式
    private const string RankingKeyPrefix = "ranking:survivor:";
    private const string RankingDataKeyPrefix = "ranking:survivor:data:";

    public SurvivorRankingCacheService(
        IConnectionMultiplexer redis,
        IDistributedLockProvider lockProvider,
        IOptions<RankingCacheSettings> cacheSettings,
        ILogger<SurvivorRankingCacheService> logger)
    {
        _redis = redis;
        _lockProvider = lockProvider;
        _defaultExpiry = TimeSpan.FromMinutes(cacheSettings.Value.DefaultExpiryMinutes);
        _logger = logger;
    }

    private IDatabase GetDatabase() => _redis.GetDatabase();

    private static string GetRankingKey(int stageId) => $"{RankingKeyPrefix}{stageId}";
    private static string GetRankingDataKey(int stageId) => $"{RankingDataKeyPrefix}{stageId}";

    public Task<List<RankingEntryDto>?> GetRankingAsync(int stageId, int limit, int offset)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = GetDatabase();
            var dataKey = GetRankingDataKey(stageId);

            // キャッシュされたランキングデータを取得
            var cachedData = await db.StringGetAsync(dataKey);
            if (cachedData.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for ranking stageId={StageId}", stageId);
                return null;
            }

            var entries = JsonHelper.TryDeserialize<List<RankingEntryDto>>(cachedData!, _logger, $"ranking cache stageId={stageId}");
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            // offset と limit でスライス
            var result = entries
                .Skip(offset)
                .Take(limit)
                .Select((e, i) => new RankingEntryDto
                {
                    Rank = offset + i + 1,
                    UserId = e.UserId,
                    UserName = e.UserName,
                    Score = e.Score,
                    ClearTime = e.ClearTime,
                    RecordedAt = e.RecordedAt,
                })
                .ToList();

            _logger.LogDebug("Cache hit for ranking stageId={StageId}, count={Count}", stageId, result.Count);
            return result;
        },
        fallback: null,
        _logger,
        nameof(GetRankingAsync));
    }

    public Task SetRankingAsync(int stageId, List<RankingEntryDto> entries, TimeSpan? expiry = null)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);
            var dataKey = GetRankingDataKey(stageId);
            var ttl = expiry ?? _defaultExpiry;

            var batch = db.CreateBatch();
            var tasks = new List<Task>();

            // Sorted Set にスコアを追加（順位取得用）
            tasks.Add(batch.KeyDeleteAsync(rankingKey));
            foreach (var entry in entries)
            {
                // スコアを負の値として保存（高いスコアが上位になるように）
                tasks.Add(batch.SortedSetAddAsync(rankingKey, entry.UserId, -entry.Score));
            }

            tasks.Add(batch.KeyExpireAsync(rankingKey, ttl));

            // ランキングデータをJSON形式で保存
            var json = JsonHelper.Serialize(entries);
            tasks.Add(batch.StringSetAsync(dataKey, json, ttl));

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Cached ranking for stageId={StageId}, count={Count}", stageId, entries.Count);
        },
        _logger,
        nameof(SetRankingAsync));
    }

    public Task<bool> AddScoreAsync(int stageId, Guid userId, int score)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);

            await using (await _lockProvider.AcquireLockAsync($"lock:ranking:survivor:{stageId}"))
            {
                // 現在のスコアを取得
                var currentScore = await db.SortedSetScoreAsync(rankingKey, userId.ToString());

                // 新しいスコアが既存のスコアより高い場合のみ更新
                // スコアは負の値で保存されているため、比較を反転
                if (currentScore.HasValue && -currentScore.Value >= score)
                {
                    return false;
                }

                // スコアを更新（負の値として保存）
                await db.SortedSetAddAsync(rankingKey, userId.ToString(), -score);
            }

            return true;
        },
        fallback: false,
        _logger,
        nameof(AddScoreAsync));
    }

    public Task<long?> GetPlayerRankAsync(int stageId, Guid userId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);

            var rank = await db.SortedSetRankAsync(rankingKey, userId.ToString());
            if (rank.HasValue)
            {
                // 0始まりを1始まりに変換
                return (long?)(rank.Value + 1);
            }

            return null;
        },
        fallback: null,
        _logger,
        nameof(GetPlayerRankAsync));
    }

    public Task InvalidateAsync(int stageId)
    {
        return ValkeyExecutor.ExecuteAsync(
        async () =>
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);
            var dataKey = GetRankingDataKey(stageId);

            await Task.WhenAll(
                db.KeyDeleteAsync(rankingKey),
                db.KeyDeleteAsync(dataKey));

            _logger.LogDebug("Invalidated cache for stageId={StageId}", stageId);
        },
        _logger,
        nameof(InvalidateAsync));
    }
}
