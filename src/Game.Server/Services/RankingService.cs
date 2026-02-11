using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;

namespace Game.Server.Services;

public class RankingService : IRankingService
{
    private readonly IRankingRepository _rankingRepository;

    public RankingService(IRankingRepository rankingRepository)
    {
        _rankingRepository = rankingRepository;
    }

    public async Task<RankingResponse> GetRankingAsync(
        int stageId, int limit, int offset)
    {
        var scores = await _rankingRepository.GetTopScoresAsync(stageId, limit, offset);

        var entries = scores.Select((s, index) => new RankingEntryResponse
        {
            Rank = offset + index + 1,
            UserId = s.User?.UserId ?? string.Empty,
            UserName = s.User?.UserName ?? string.Empty,
            Score = s.Score,
            ClearTime = s.ClearTime,
            RecordedAt = new DateTimeOffset(s.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        }).ToList();

        return new RankingResponse
        {
            StageId = stageId,
            TotalCount = entries.Count,
            Entries = entries,
        };
    }

    public async Task<RankingEntryResponse?> GetUserRankAsync(
        int stageId, Guid userId)
    {
        var bestScore = await _rankingRepository.GetUserBestScoreAsync(stageId, userId);
        if (bestScore == null)
        {
            return null;
        }

        int rank = await _rankingRepository.GetUserRankAsync(stageId, userId);

        return new RankingEntryResponse
        {
            Rank = rank,
            UserId = bestScore.User?.UserId ?? string.Empty,
            UserName = bestScore.User?.UserName ?? string.Empty,
            Score = bestScore.Score,
            ClearTime = bestScore.ClearTime,
            RecordedAt = new DateTimeOffset(bestScore.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
    }
}
