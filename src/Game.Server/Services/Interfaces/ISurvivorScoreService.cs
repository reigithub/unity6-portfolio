using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface ISurvivorScoreService
{
    Task<Result<SurvivorScoreSubmitResponse, ApiError>> SubmitScoreAsync(Guid userId, SubmitSurvivorScoreRequest request);

    Task<List<SurvivorScoreHistoryEntry>> GetUserScoresAsync(Guid userId, int? stageId, int limit);
}

public class SurvivorScoreHistoryEntry
{
    public long Id { get; set; }

    public int StageId { get; set; }

    public int Score { get; set; }

    public float ClearTime { get; set; }

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }

    public long RecordedAt { get; set; }
}
