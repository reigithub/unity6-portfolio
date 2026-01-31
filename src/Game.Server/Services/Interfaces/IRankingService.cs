using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IRankingService
{
    Task<RankingResponse> GetRankingAsync(string gameMode, int stageId, int limit, int offset);

    Task<RankingEntryResponse?> GetUserRankAsync(string gameMode, int stageId, string userId);
}
