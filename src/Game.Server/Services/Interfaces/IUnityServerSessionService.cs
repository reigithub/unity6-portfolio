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
    /// <param name="matchId">割り当てるマッチID。</param>
    /// <param name="stageId">ステージID。</param>
    /// <param name="expectedPlayers">期待プレイヤー数。</param>
    /// <exception cref="InvalidOperationException">空き DS が存在しない場合。</exception>
    Task AssignSessionAsync(string matchId, int stageId, int expectedPlayers);
}
