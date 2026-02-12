using System.Text.Json;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using StackExchange.Redis;

namespace Game.Server.Services;

/// <summary>
/// Valkey (Redis互換) を使用したランキングキャッシュサービス
/// Sorted Set でランキングを管理し、高速な順位取得を実現
/// </summary>
public class ValkeySurvivorRankingCacheService : ISurvivorRankingCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeySurvivorRankingCacheService> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(5);

    // キー形式
    private const string RankingKeyPrefix = "ranking:survivor:";
    private const string RankingDataKeyPrefix = "ranking:survivor:data:";

    public ValkeySurvivorRankingCacheService(
        IConnectionMultiplexer redis,
        ILogger<ValkeySurvivorRankingCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase GetDatabase() => _redis.GetDatabase();

    private static string GetRankingKey(int stageId) => $"{RankingKeyPrefix}{stageId}";
    private static string GetRankingDataKey(int stageId) => $"{RankingDataKeyPrefix}{stageId}";

    public async Task<List<RankingEntryResponse>?> GetRankingAsync(int stageId, int limit, int offset)
    {
        try
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

            var entries = JsonSerializer.Deserialize<List<RankingEntryResponse>>(cachedData!);
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            // offset と limit でスライス
            var result = entries
                .Skip(offset)
                .Take(limit)
                .Select((e, i) => new RankingEntryResponse
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
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, falling back to database");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ranking from cache for stageId={StageId}", stageId);
            return null;
        }
    }

    public async Task SetRankingAsync(int stageId, List<RankingEntryResponse> entries, TimeSpan? expiry = null)
    {
        try
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
            var json = JsonSerializer.Serialize(entries);
            tasks.Add(batch.StringSetAsync(dataKey, json, ttl));

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Cached ranking for stageId={StageId}, count={Count}", stageId, entries.Count);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, skipping cache set");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting ranking cache for stageId={StageId}", stageId);
        }
    }

    public async Task<bool> AddScoreAsync(int stageId, Guid userId, int score)
    {
        try
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);

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
            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, skipping score add");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding score to cache for stageId={StageId}, userId={UserId}", stageId, userId);
            return false;
        }
    }

    public async Task<long?> GetPlayerRankAsync(int stageId, Guid userId)
    {
        try
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);

            var rank = await db.SortedSetRankAsync(rankingKey, userId.ToString());
            if (rank.HasValue)
            {
                // 0始まりを1始まりに変換
                return rank.Value + 1;
            }

            return null;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, returning null for rank");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player rank from cache for stageId={StageId}, userId={UserId}", stageId, userId);
            return null;
        }
    }

    public async Task InvalidateAsync(int stageId)
    {
        try
        {
            var db = GetDatabase();
            var rankingKey = GetRankingKey(stageId);
            var dataKey = GetRankingDataKey(stageId);

            await Task.WhenAll(
                db.KeyDeleteAsync(rankingKey),
                db.KeyDeleteAsync(dataKey));

            _logger.LogDebug("Invalidated cache for stageId={StageId}", stageId);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, skipping cache invalidation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for stageId={StageId}", stageId);
        }
    }
}
