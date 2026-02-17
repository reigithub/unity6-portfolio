using Game.Library.Shared.Dto;

namespace Game.Server.Services.Interfaces;

/// <summary>
/// Survivor ランキングキャッシュサービスインターフェース
/// Valkey (Redis互換) を使用してランキングデータをキャッシュ
/// </summary>
public interface ISurvivorRankingCacheService
{
    /// <summary>
    /// キャッシュからランキングを取得
    /// </summary>
    /// <param name="stageId">ステージID</param>
    /// <param name="limit">取得件数</param>
    /// <param name="offset">オフセット</param>
    /// <returns>キャッシュがある場合はランキングエントリのリスト、ない場合はnull</returns>
    Task<List<RankingEntryDto>?> GetRankingAsync(int stageId, int limit, int offset);

    /// <summary>
    /// ランキングをキャッシュに保存
    /// </summary>
    /// <param name="stageId">ステージID</param>
    /// <param name="entries">ランキングエントリのリスト</param>
    /// <param name="expiry">有効期限（デフォルト5分）</param>
    Task SetRankingAsync(int stageId, List<RankingEntryDto> entries, TimeSpan? expiry = null);

    /// <summary>
    /// スコアをSorted Setに追加
    /// </summary>
    /// <param name="stageId">ステージID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="score">スコア</param>
    /// <returns>追加成功した場合true</returns>
    Task<bool> AddScoreAsync(int stageId, Guid userId, int score);

    /// <summary>
    /// プレイヤーの順位を取得
    /// </summary>
    /// <param name="stageId">ステージID</param>
    /// <param name="userId">ユーザーID</param>
    /// <returns>順位（1始まり）、存在しない場合はnull</returns>
    Task<long?> GetPlayerRankAsync(int stageId, Guid userId);

    /// <summary>
    /// 指定ステージのキャッシュを無効化
    /// </summary>
    /// <param name="stageId">ステージID</param>
    Task InvalidateAsync(int stageId);
}
