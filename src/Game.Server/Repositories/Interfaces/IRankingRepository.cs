using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IRankingRepository
{
    Task<List<UserScore>> GetTopScoresAsync(string gameMode, int stageId, int limit, int offset);

    Task<UserScore?> GetUserBestScoreAsync(string gameMode, int stageId, string userId);

    Task<int> GetUserRankAsync(string gameMode, int stageId, string userId);
}
