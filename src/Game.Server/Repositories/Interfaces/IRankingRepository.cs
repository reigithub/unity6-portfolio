using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IRankingRepository
{
    Task<List<SurvivorScore>> GetTopScoresAsync(int stageId, int limit, int offset);

    Task<SurvivorScore?> GetUserBestScoreAsync(int stageId, Guid userId);

    Task<int> GetUserRankAsync(int stageId, Guid userId);
}
