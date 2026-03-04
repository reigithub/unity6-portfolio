namespace Game.Realtime.Services;

/// <summary>
/// マッチメイキングキューサービスインターフェース
/// </summary>
public interface IMatchmakingQueueService
{
    /// <summary>
    /// プレイヤーをマッチメイキングキューに追加
    /// stageId &lt;= 0 の場合は "any" キューに追加
    /// </summary>
    Task EnqueuePlayerAsync(string userId, string gameMode, int stageId, int matchSize);

    /// <summary>
    /// プレイヤーをマッチメイキングキューから削除
    /// stageId &lt;= 0 の場合は "any" キューから削除
    /// </summary>
    Task DequeuePlayerAsync(string userId, string gameMode, int stageId);

    /// <summary>
    /// 指定ゲームモード・ステージのキュー内プレイヤー数を取得
    /// </summary>
    Task<int> GetQueueCountAsync(string gameMode, int stageId);

    /// <summary>
    /// キューから上位 N 人を原子的に取得（ZPOPMIN）
    /// </summary>
    Task<string[]> DequeueTopPlayersAsync(string gameMode, int stageId, int count);

    /// <summary>
    /// 指定ゲームモードでアクティブな stageId 一覧を取得（"any" 含む）
    /// </summary>
    Task<string[]> GetActiveStageKeysAsync(string gameMode);

    /// <summary>
    /// プレイヤーの希望 matchSize を取得
    /// </summary>
    Task<int> GetPlayerMatchSizeAsync(string userId);

    /// <summary>
    /// プレイヤーメタデータ（matchSize等）をクリーンアップ
    /// </summary>
    Task CleanupPlayerAsync(string userId);
}
