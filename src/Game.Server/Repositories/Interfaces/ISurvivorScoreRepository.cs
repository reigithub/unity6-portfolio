using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface ISurvivorScoreRepository
{
    Task<SurvivorScore> AddAsync(SurvivorScore score);

    Task<List<SurvivorScore>> GetUserScoresAsync(Guid userId, int? stageId, int limit);
}
