using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;

namespace Game.Server.Services;

public class SurvivorScoreService : ISurvivorScoreService
{
    private readonly ISurvivorScoreRepository _scoreRepository;
    private readonly IRankingRepository _rankingRepository;

    public SurvivorScoreService(ISurvivorScoreRepository scoreRepository, IRankingRepository rankingRepository)
    {
        _scoreRepository = scoreRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<Result<SurvivorScoreSubmitResponse, ApiError>> SubmitScoreAsync(
        Guid userId, SubmitSurvivorScoreRequest request)
    {
        var previousBest = await _rankingRepository.GetUserBestScoreAsync(
            request.StageId, userId);

        var score = new SurvivorScore
        {
            UserId = userId,
            StageId = request.StageId,
            Score = request.Score,
            ClearTime = request.ClearTime,
            WaveReached = request.WaveReached,
            EnemiesDefeated = request.EnemiesDefeated,
        };

        var saved = await _scoreRepository.AddAsync(score);

        bool isNewBest = previousBest == null || request.Score > previousBest.Score;

        int currentRank = await _rankingRepository.GetUserRankAsync(
            request.StageId, userId);

        return new SurvivorScoreSubmitResponse
        {
            ScoreId = saved.Id,
            IsNewBest = isNewBest,
            CurrentRank = currentRank,
        };
    }

    public async Task<List<SurvivorScoreHistoryEntry>> GetUserScoresAsync(
        Guid userId, int? stageId, int limit)
    {
        var scores = await _scoreRepository.GetUserScoresAsync(userId, stageId, limit);

        return scores.Select(s => new SurvivorScoreHistoryEntry
        {
            Id = s.Id,
            StageId = s.StageId,
            Score = s.Score,
            ClearTime = s.ClearTime,
            WaveReached = s.WaveReached,
            EnemiesDefeated = s.EnemiesDefeated,
            RecordedAt = new DateTimeOffset(s.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        }).ToList();
    }
}
