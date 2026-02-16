using System.Threading.Tasks;
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
    }

    /// <summary>
    /// ロビーHub サーバー送信インターフェース（StreamingHub）
    /// </summary>
    public interface ILobbyHub : IStreamingHub<ILobbyHub, ILobbyHubReceiver>
    {
        /// <summary>
        /// ロビーに参加
        /// </summary>
        ValueTask JoinAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーから退出
        /// </summary>
        ValueTask LeaveAsync();

        /// <summary>
        /// ロビーにメッセージ送信
        /// </summary>
        ValueTask SendMessageAsync(string message);

        /// <summary>
        /// ロビーのプレイヤー一覧を取得
        /// </summary>
        ValueTask<string[]> GetPlayersAsync();
    }
}
