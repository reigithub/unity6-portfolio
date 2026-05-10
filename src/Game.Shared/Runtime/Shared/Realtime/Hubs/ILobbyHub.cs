using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using MagicOnion;

namespace Game.Library.Shared.Realtime.Hubs
{
    /// <summary>
    /// ロビーHub クライアント受信インターフェース
    /// </summary>
    public interface ILobbyHubReceiver
    {
        /// <summary>
        /// プレイヤーがロビーに参加した通知
        /// </summary>
        void OnPlayerJoined(string userId, string playerName);

        /// <summary>
        /// プレイヤーがロビーから退出した通知
        /// </summary>
        void OnPlayerLeft(string userId, string playerName);

        /// <summary>
        /// ロビーメッセージ受信
        /// </summary>
        void OnMessageReceived(string userId, string playerName, string message);

        /// <summary>
        /// ロビーが閉じられた通知
        /// </summary>
        void OnLobbyClosed(string reason);

        /// <summary>
        /// プレイヤーのレディ状態変更通知
        /// </summary>
        void OnPlayerReadyChanged(string userId, bool isReady);

        /// <summary>
        /// ゲーム開始通知。Topology に応じて DS フィールドまたは P2P フィールドが populate される。
        /// </summary>
        void OnGameStarting(GameSessionStartInfo info);

        /// <summary>
        /// ステージ変更通知
        /// </summary>
        void OnStageChanged(int stageId, string changedByUserId);
    }

    /// <summary>
    /// ロビーHub サーバー送信インターフェース（StreamingHub）
    /// ロビー参加/退出は Unary ILobbyService 経由。Hub はリアルタイムイベント専用。
    /// </summary>
    public interface ILobbyHub : IStreamingHub<ILobbyHub, ILobbyHubReceiver>
    {
        /// <summary>
        /// ロビーに接続（Hub グループ参加のみ、ロビー参加は Unary 経由）
        /// </summary>
        ValueTask ConnectAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーから退出
        /// </summary>
        ValueTask LeaveAsync();

        /// <summary>
        /// ロビーにメッセージ送信
        /// </summary>
        ValueTask SendMessageAsync(string message);

        /// <summary>
        /// レディ状態を設定
        /// </summary>
        ValueTask SetReadyAsync(bool isReady);

        /// <summary>
        /// ステージを変更（ホストのみ）
        /// </summary>
        ValueTask SetStageAsync(int stageId);

        /// <summary>
        /// P2P Host が Photon セッション作成 + GameState Spawn 完了を Hub に通知する。
        /// Hub はこの通知を受けてから残りクライアントに OnGameStarting を broadcast する。
        /// Host 以外が呼び出すと PermissionDenied。
        /// </summary>
        ValueTask NotifyHostReadyAsync();
    }
}
