using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface ISurvivorScoreService
{
    Task<Result<SurvivorScoreSubmitResponse, ApiError>> SubmitScoreAsync(string userId, ScoreSubmitDto request);

    Task<List<SurvivorScoreHistoryEntry>> GetUserScoresAsync(string userId, int? stageId, int limit);
}
