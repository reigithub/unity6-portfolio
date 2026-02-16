using System;
using System.Threading.Tasks;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// ロビーサービスインターフェース（クライアント側）
    /// </summary>
    public interface ILobbyService : IDisposable
    {
        /// <summary>
        /// ロビーに接続済みかどうか
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// ロビーに参加
        /// </summary>
        Task JoinLobbyAsync(string lobbyId, string playerName);

        /// <summary>
        /// ロビーから退出
        /// </summary>
        Task LeaveLobbyAsync();

        /// <summary>
        /// メッセージ送信
        /// </summary>
        Task SendMessageAsync(string message);

        /// <summary>
        /// プレイヤー参加イベント
        /// </summary>
        event Action<string, string> OnPlayerJoined;

        /// <summary>
        /// プレイヤー退出イベント
        /// </summary>
        event Action<string, string> OnPlayerLeft;

        /// <summary>
        /// メッセージ受信イベント
        /// </summary>
        event Action<string, string, string> OnMessageReceived;
    }
}
