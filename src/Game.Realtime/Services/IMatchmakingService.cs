namespace Game.Realtime.Services;

/// <summary>
/// マッチメイキングサービスインターフェース
/// </summary>
public interface IMatchmakingService
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
}
