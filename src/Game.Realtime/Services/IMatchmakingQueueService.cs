namespace Game.Realtime.Services;

/// <summary>
/// マッチメイキングキューサービスインターフェース
/// </summary>
public interface IMatchmakingQueueService
{
    /// <summary>
    /// プレイヤーをマッチメイキングキューに追加
    /// </summary>
    Task EnqueuePlayerAsync(string userId, string gameMode);

    /// <summary>
    /// プレイヤーをマッチメイキングキューから削除
    /// </summary>
    Task DequeuePlayerAsync(string userId, string gameMode);

    /// <summary>
    /// 指定ゲームモードのキュー内プレイヤー数を取得
    /// </summary>
    Task<int> GetQueueCountAsync(string gameMode);

    /// <summary>
    /// キューから上位 N 人を原子的に取得（ZPOPMIN）
    /// </summary>
    Task<string[]> DequeueTopPlayersAsync(string gameMode, int count);
}
