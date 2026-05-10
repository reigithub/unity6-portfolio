namespace Game.Server.Services.Interfaces;

/// <summary>
/// Dedicated Server へのセッション割り当てサービスのインターフェース。
/// 空き DS を選択し、セッション開始を指示する。
/// </summary>
public interface IUnityServerSessionService
{
    /// <summary>
    /// 空き DS を選択し、セッション作成を指示する。
    /// DS に POST /session/start を送信し、ステータスを active に更新する。
    /// </summary>
    /// <param name="sessionName">Fusion セッション名（SessionName）。</param>
    /// <param name="stageId">ステージID。</param>
    /// <param name="playerCount">プレイヤー数。</param>
    /// <param name="hostUserId">ロビーホストの UserId。DS 側で「手動ポーズ操作の権限を持つ Client」の判定に使用。MP 以外 (SP) では空文字。</param>
    /// <returns>割り当てた DS の情報。クライアントへの接続先通知に使用する。</returns>
    /// <exception cref="InvalidOperationException">空き DS が存在しない場合。</exception>
    Task<DsInfo> AssignSessionAsync(string sessionName, int stageId, int playerCount, string hostUserId = "");
}
