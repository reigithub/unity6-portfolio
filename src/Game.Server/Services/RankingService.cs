using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;
using Medallion.Threading;

namespace Game.Server.Services;

public class RankingService : IRankingService
{
    private readonly IRankingRepository _rankingRepository;
    private readonly ISurvivorRankingCacheService _cacheService;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<RankingService> _logger;

    public RankingService(
        IRankingRepository rankingRepository,
        ISurvivorRankingCacheService cacheService,
        IDistributedLockProvider lockProvider,
        ILogger<RankingService> logger)
    {
        _rankingRepository = rankingRepository;
        _cacheService = cacheService;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    public async Task<RankingResponse> GetRankingAsync(
        int stageId, int limit, int offset)
    {
        // キャッシュから取得を試みる
        var cachedEntries = await _cacheService.GetRankingAsync(stageId, limit, offset);
        if (cachedEntries != null)
        {
            _logger.LogDebug("Returning cached ranking for stageId={StageId}", stageId);
            return new RankingResponse
            {
                StageId = stageId,
                TotalCount = cachedEntries.Count,
                Entries = cachedEntries,
            };
        }

        // キャッシュミス: ロック取得してDBから取得（スタンピード防止）
        await using (await _lockProvider.AcquireLockAsync($"lock:ranking:cache:{stageId}"))
        {
            // ロック取得後にキャッシュを再チェック（double-checked locking）
            cachedEntries = await _cacheService.GetRankingAsync(stageId, limit, offset);
            if (cachedEntries != null)
            {
                _logger.LogDebug("Cache hit after lock for stageId={StageId}", stageId);
                return new RankingResponse
                {
                    StageId = stageId,
                    TotalCount = cachedEntries.Count,
                    Entries = cachedEntries,
                };
            }

            _logger.LogDebug("Cache miss, fetching ranking from database for stageId={StageId}", stageId);
            var scores = await _rankingRepository.GetTopScoresAsync(stageId, limit, offset);

            var entries = scores.Select((s, index) => new RankingEntryDto
            {
                Rank = offset + index + 1,
                UserId = s.User?.UserId ?? string.Empty,
                UserName = s.User?.UserName ?? string.Empty,
                Score = s.Score,
                ClearTime = s.ClearTime,
                RecordedAt = new DateTimeOffset(s.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            }).ToList();

            // キャッシュに保存（エラーはキャッシュサービス内で処理済み）
            await _cacheService.SetRankingAsync(stageId, entries);

            return new RankingResponse
            {
                StageId = stageId,
                TotalCount = entries.Count,
                Entries = entries,
            };
        }
    }

    public async Task<RankingEntryDto?> GetUserRankAsync(
        int stageId, Guid userId)
    {
        var bestScore = await _rankingRepository.GetUserBestScoreAsync(stageId, userId);
        if (bestScore == null)
        {
            return null;
        }

        // キャッシュから順位を取得を試みる
        var cachedRank = await _cacheService.GetPlayerRankAsync(stageId, userId);
        int rank;

        if (cachedRank.HasValue)
        {
            rank = (int)cachedRank.Value;
        }
        else
        {
            // キャッシュミス: DBから取得
            rank = await _rankingRepository.GetUserRankAsync(stageId, userId);
        }

        return new RankingEntryDto
        {
            Rank = rank,
            UserId = bestScore.User?.UserId ?? string.Empty,
            UserName = bestScore.User?.UserName ?? string.Empty,
            Score = bestScore.Score,
            ClearTime = bestScore.ClearTime,
            RecordedAt = new DateTimeOffset(bestScore.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
    }

    /// <summary>
    /// スコア送信後にキャッシュを無効化
    /// </summary>
    public async Task InvalidateCacheAsync(int stageId)
    {
        await _cacheService.InvalidateAsync(stageId);
    }
}
