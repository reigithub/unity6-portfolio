using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Exceptions;

namespace Game.Server.Validation;

public interface ISurvivorValidator
{
    void ValidateScoreSubmit(ScoreSubmitDto request);
    void ValidateStageId(int stageId);
    void ValidateLimit(int limit);
    void ValidateOffset(int offset);
}

public class SurvivorValidator : ISurvivorValidator
{
    private const float ClearTimeBufferSeconds = 5f;
    private const int MaxLimit = 1000;
    private const int MaxOffset = 100000;

    private readonly IMasterDataService _masterData;
    private readonly ILogger<SurvivorValidator> _logger;

    public SurvivorValidator(IMasterDataService masterData, ILogger<SurvivorValidator> logger)
    {
        _masterData = masterData;
        _logger = logger;
    }

    public void ValidateScoreSubmit(ScoreSubmitDto request)
    {
        // 1. ステージ存在チェック
        if (!_masterData.MemoryDatabase.SurvivorStageMasterTable.TryFindById(request.StageId, out var stage))
        {
            _logger.LogWarning("Score validation failed: StageId {StageId} not found", request.StageId);
            throw new ErrorException("INVALID_SCORE", "Invalid stage ID.");
        }

        // 2. ClearTime が正の値か
        if (request.ClearTime <= 0)
        {
            _logger.LogWarning("Score validation failed: ClearTime {ClearTime} is not positive", request.ClearTime);
            throw new ErrorException("INVALID_SCORE", "Clear time must be positive.");
        }

        // 3. ClearTime が TimeLimit 以内か（バッファ付き）
        if (request.ClearTime > stage.TimeLimit + ClearTimeBufferSeconds)
        {
            _logger.LogWarning(
                "Score validation failed: ClearTime {ClearTime} exceeds TimeLimit {TimeLimit} for StageId {StageId}",
                request.ClearTime, stage.TimeLimit, request.StageId);
            throw new ErrorException("INVALID_SCORE", "Clear time exceeds stage time limit.");
        }

        // 4. スコアが負数でないか
        if (request.Score < 0)
        {
            _logger.LogWarning("Score validation failed: negative score {Score}", request.Score);
            throw new ErrorException("INVALID_SCORE", "Score must not be negative.");
        }

        // 5. EnemiesDefeated が負数でないか
        if (request.EnemiesDefeated < 0)
        {
            _logger.LogWarning("Score validation failed: negative EnemiesDefeated {EnemiesDefeated}", request.EnemiesDefeated);
            throw new ErrorException("INVALID_SCORE", "Enemies defeated must not be negative.");
        }

        // 6. WaveReached 範囲チェック
        var waves = _masterData.MemoryDatabase.SurvivorStageWaveMasterTable.FindByStageId(request.StageId);
        int maxWaveNumber = 0;
        foreach (var w in waves)
        {
            if (w.WaveNumber > maxWaveNumber) maxWaveNumber = w.WaveNumber;
        }

        if (request.WaveReached < 0 || request.WaveReached > maxWaveNumber)
        {
            _logger.LogWarning(
                "Score validation failed: WaveReached {WaveReached} out of range [0, {MaxWave}] for StageId {StageId}",
                request.WaveReached, maxWaveNumber, request.StageId);
            throw new ErrorException("INVALID_SCORE", "Wave reached is out of valid range.");
        }

        // 7. Score 上限チェック（理論最大: 各ウェーブで remainingTime=TimeLimit, hpRatio=1.0 を仮定）
        long scoreUpperBound = 0;
        foreach (var w in waves)
        {
            if (w.WaveNumber <= request.WaveReached)
                scoreUpperBound += (long)stage.TimeLimit * (w.ScoreMultiplier > 0 ? w.ScoreMultiplier : 100);
        }

        if (request.Score > scoreUpperBound)
        {
            _logger.LogWarning(
                "Score rejected: {Score} exceeds upper bound {UpperBound} for Stage {StageId}",
                request.Score, scoreUpperBound, request.StageId);
            throw new ErrorException("INVALID_SCORE", "Score exceeds maximum possible value.");
        }
    }

    public void ValidateStageId(int stageId)
    {
        if (stageId <= 0)
        {
            throw new ErrorException("INVALID_INPUT", "Stage ID must be greater than 0.");
        }
    }

    public void ValidateLimit(int limit)
    {
        if (limit < 1 || limit > MaxLimit)
        {
            throw new ErrorException("INVALID_INPUT",
                $"Limit must be between 1 and {MaxLimit}.");
        }
    }

    public void ValidateOffset(int offset)
    {
        if (offset < 0 || offset > MaxOffset)
        {
            throw new ErrorException("INVALID_INPUT",
                $"Offset must be between 0 and {MaxOffset}.");
        }
    }
}
