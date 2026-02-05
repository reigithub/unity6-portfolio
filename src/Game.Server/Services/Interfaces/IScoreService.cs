using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IScoreService
{
    Task<Result<ScoreSubmitResponse, ApiError>> SubmitScoreAsync(Guid userId, SubmitScoreRequest request);

    Task<List<ScoreHistoryEntry>> GetUserScoresAsync(Guid userId, string? gameMode, int? stageId, int limit);
}

public class ScoreHistoryEntry
{
    public long Id { get; set; }

    public string GameMode { get; set; } = string.Empty;

    public int StageId { get; set; }

    public int Score { get; set; }

    public float ClearTime { get; set; }

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }

    public long RecordedAt { get; set; }
}
