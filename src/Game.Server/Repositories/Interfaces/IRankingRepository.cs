using Game.Server.Entities;

namespace Game.Server.Repositories.Interfaces;

public interface IRankingRepository
{
    Task<List<ScoreEntity>> GetTopScoresAsync(string gameMode, int stageId, int limit, int offset);

    Task<ScoreEntity?> GetUserBestScoreAsync(string gameMode, int stageId, string userId);

    Task<int> GetUserRankAsync(string gameMode, int stageId, string userId);
}
