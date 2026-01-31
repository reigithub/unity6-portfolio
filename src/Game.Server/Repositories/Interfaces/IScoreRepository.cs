using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IScoreRepository
{
    Task<UserScore> AddAsync(UserScore score);

    Task<List<UserScore>> GetUserScoresAsync(string userId, string? gameMode, int? stageId, int limit);
}
