using Game.Server.Entities;

namespace Game.Server.Repositories.Interfaces;

public interface IScoreRepository
{
    Task<ScoreEntity> AddAsync(ScoreEntity score);

    Task<List<ScoreEntity>> GetUserScoresAsync(string userId, string? gameMode, int? stageId, int limit);
}
