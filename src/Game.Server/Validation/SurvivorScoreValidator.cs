using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Exceptions;

namespace Game.Server.Validation;

public interface ISurvivorScoreValidator
{
    void Validate(ScoreSubmitDto request);
}

public class SurvivorScoreValidator : ISurvivorScoreValidator
{
    private const float ClearTimeBufferSeconds = 5f;

    private readonly IMasterDataService _masterData;
    private readonly ILogger<SurvivorScoreValidator> _logger;

    public SurvivorScoreValidator(IMasterDataService masterData, ILogger<SurvivorScoreValidator> logger)
    {
        _masterData = masterData;
        _logger = logger;
    }

    public void Validate(ScoreSubmitDto request)
    {
        // ステージ存在チェック
        if (!_masterData.MemoryDatabase.SurvivorStageMasterTable.TryFindById(request.StageId, out var stage))
        {
            _logger.LogWarning("Score validation failed: StageId {StageId} not found", request.StageId);
            throw new ErrorException("INVALID_SCORE", "Invalid stage ID.");
        }

        // スコアが負数でないか
        if (request.Score < 0)
        {
            _logger.LogWarning("Score validation failed: negative score {Score}", request.Score);
            throw new ErrorException("INVALID_SCORE", "Score must not be negative.");
        }

        // EnemiesDefeated が負数でないか
        if (request.EnemiesDefeated < 0)
        {
            _logger.LogWarning("Score validation failed: negative EnemiesDefeated {EnemiesDefeated}", request.EnemiesDefeated);
            throw new ErrorException("INVALID_SCORE", "Enemies defeated must not be negative.");
        }

        // ClearTime が TimeLimit 以内か（バッファ付き）
        if (request.ClearTime > stage.TimeLimit + ClearTimeBufferSeconds)
        {
            _logger.LogWarning(
                "Score validation failed: ClearTime {ClearTime} exceeds TimeLimit {TimeLimit} for StageId {StageId}",
                request.ClearTime, stage.TimeLimit, request.StageId);
            throw new ErrorException("INVALID_SCORE", "Clear time exceeds stage time limit.");
        }
    }
}
