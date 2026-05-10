using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;
using Game.Server.Validation;

namespace Game.Server.Services;

public class SurvivorScoreService : ISurvivorScoreService
{
    private readonly ISurvivorScoreRepository _scoreRepository;
    private readonly IRankingRepository _rankingRepository;
    private readonly IRankingService _rankingService;
    private readonly ISurvivorValidator _survivorValidator;
    private readonly IUserRepository _userRepository;

    public SurvivorScoreService(
        ISurvivorScoreRepository scoreRepository,
        IRankingRepository rankingRepository,
        IRankingService rankingService,
        ISurvivorValidator survivorValidator,
        IUserRepository userRepository)
    {
        _scoreRepository = scoreRepository;
        _rankingRepository = rankingRepository;
        _rankingService = rankingService;
        _survivorValidator = survivorValidator;
        _userRepository = userRepository;
    }

    public async Task<Result<SurvivorScoreSubmitResponse, ApiError>> SubmitScoreAsync(
        string userId, ScoreSubmitDto request)
    {
        _survivorValidator.ValidateScoreSubmit(request);
        // ErrorException("INVALID_SCORE") は ExceptionHandlingMiddleware が処理

        var user = await _userRepository.GetByUserIdAsync(userId);
        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        var previousBest = await _rankingRepository.GetUserBestScoreAsync(
            request.StageId, user.Id);

        var score = new SurvivorScore
        {
            UserId = user.Id,
            StageId = request.StageId,
            Score = request.Score,
            ClearTime = request.ClearTime,
            WaveReached = request.WaveReached,
            EnemiesDefeated = request.EnemiesDefeated,
        };

        var saved = await _scoreRepository.AddAsync(score);

        bool isNewBest = previousBest == null || request.Score > previousBest.Score;

        // スコア送信後にキャッシュを無効化
        if (isNewBest)
        {
            await _rankingService.InvalidateCacheAsync(request.StageId);
        }

        int currentRank = await _rankingRepository.GetUserRankAsync(
            request.StageId, user.Id);

        return new SurvivorScoreSubmitResponse
        {
            ScoreId = saved.Id,
            IsNewBest = isNewBest,
            CurrentRank = currentRank,
        };
    }

    public async Task<List<SurvivorScoreHistoryEntry>> GetUserScoresAsync(
        string userId, int? stageId, int limit)
    {
        var user = await _userRepository.GetByUserIdAsync(userId);
        if (user == null)
        {
            return new List<SurvivorScoreHistoryEntry>();
        }
        var scores = await _scoreRepository.GetUserScoresAsync(user.Id, stageId, limit);

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
