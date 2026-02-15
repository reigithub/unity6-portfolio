using Game.Server.Dto.Requests;
using Game.Server.Services.Interfaces;
using Game.Server.Services.Validations;

namespace Game.Server.Services;

public interface ISurvivorScoreValidationService
{
    RequestValidationResult Validate(SubmitSurvivorScoreRequest request);
}

public class SurvivorScoreValidationService : ISurvivorScoreValidationService
{
    private const float ClearTimeBufferSeconds = 5f;

    private readonly IMasterDataService _masterData;
    private readonly ILogger<SurvivorScoreValidationService> _logger;

    public SurvivorScoreValidationService(IMasterDataService masterData, ILogger<SurvivorScoreValidationService> logger)
    {
        _masterData = masterData;
        _logger = logger;
    }

    public RequestValidationResult Validate(SubmitSurvivorScoreRequest request)
    {
        // ステージ存在チェック
        if (!_masterData.MemoryDatabase.SurvivorStageMasterTable.TryFindById(request.StageId, out var stage))
        {
            _logger.LogWarning("Score validation failed: StageId {StageId} not found", request.StageId);
            return RequestValidationResult.Failure("Invalid stage ID.");
        }

        // スコアが負数でないか
        if (request.Score < 0)
        {
            _logger.LogWarning("Score validation failed: negative score {Score}", request.Score);
            return RequestValidationResult.Failure("Score must not be negative.");
        }

        // EnemiesDefeated が負数でないか
        if (request.EnemiesDefeated < 0)
        {
            _logger.LogWarning("Score validation failed: negative EnemiesDefeated {EnemiesDefeated}", request.EnemiesDefeated);
            return RequestValidationResult.Failure("Enemies defeated must not be negative.");
        }

        // ClearTime が TimeLimit 以内か（バッファ付き）
        if (request.ClearTime > stage.TimeLimit + ClearTimeBufferSeconds)
        {
            _logger.LogWarning(
                "Score validation failed: ClearTime {ClearTime} exceeds TimeLimit {TimeLimit} for StageId {StageId}",
                request.ClearTime, stage.TimeLimit, request.StageId);
            return RequestValidationResult.Failure("Clear time exceeds stage time limit.");
        }

        return RequestValidationResult.Success();
    }
}
