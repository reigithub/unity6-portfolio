using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;

namespace Game.Server.Services;

public class ScoreService : IScoreService
{
    private static readonly HashSet<string> ValidGameModes = new(StringComparer.Ordinal)
    {
        "Survivor",
        "ScoreTimeAttack",
    };

    private readonly IScoreRepository _scoreRepository;
    private readonly IRankingRepository _rankingRepository;

    public ScoreService(IScoreRepository scoreRepository, IRankingRepository rankingRepository)
    {
        _scoreRepository = scoreRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<Result<ScoreSubmitResponse, ApiError>> SubmitScoreAsync(
        string userId, SubmitScoreRequest request)
    {
        if (!ValidGameModes.Contains(request.GameMode))
        {
            return new ApiError(
                $"Invalid game mode: {request.GameMode}. Must be 'Survivor' or 'ScoreTimeAttack'.",
                "INVALID_GAME_MODE",
                StatusCodes.Status400BadRequest);
        }

        var previousBest = await _rankingRepository.GetUserBestScoreAsync(
            request.GameMode, request.StageId, userId);

        var score = new UserScore
        {
            UserId = userId,
            GameMode = request.GameMode,
            StageId = request.StageId,
            Score = request.Score,
            ClearTime = request.ClearTime,
            WaveReached = request.WaveReached,
            EnemiesDefeated = request.EnemiesDefeated,
        };

        var saved = await _scoreRepository.AddAsync(score);

        bool isNewBest = previousBest == null || request.Score > previousBest.Score;

        int currentRank = await _rankingRepository.GetUserRankAsync(
            request.GameMode, request.StageId, userId);

        return new ScoreSubmitResponse
        {
            ScoreId = saved.Id,
            IsNewBest = isNewBest,
            CurrentRank = currentRank,
        };
    }

    public async Task<List<ScoreHistoryEntry>> GetUserScoresAsync(
        string userId, string? gameMode, int? stageId, int limit)
    {
        var scores = await _scoreRepository.GetUserScoresAsync(userId, gameMode, stageId, limit);

        return scores.Select(s => new ScoreHistoryEntry
        {
            Id = s.Id,
            GameMode = s.GameMode,
            StageId = s.StageId,
            Score = s.Score,
            ClearTime = s.ClearTime,
            WaveReached = s.WaveReached,
            EnemiesDefeated = s.EnemiesDefeated,
            RecordedAt = new DateTimeOffset(s.RecordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        }).ToList();
    }
}
