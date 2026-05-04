using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IRankingService
{
    Task<RankingResponse> GetRankingAsync(int stageId, int limit, int offset);

    Task<RankingEntryDto?> GetUserRankAsync(int stageId, string userId);

    /// <summary>
    /// スコア送信後にキャッシュを無効化
    /// </summary>
    Task InvalidateCacheAsync(int stageId);
}
