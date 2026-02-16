using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// チャットクライアントインターフェース（Hub ベース）
    /// </summary>
    public interface IChatClient : IDisposable
    {
        /// <summary>
        /// チャットルームに接続済みかどうか
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// チャットルームに参加
        /// </summary>
        Task JoinAsync(string roomId, string playerName);

        /// <summary>
        /// チャットルームから退出
        /// </summary>
        Task LeaveAsync();

        /// <summary>
        /// メッセージ送信
        /// </summary>
        Task SendMessageAsync(string content);

        /// <summary>
        /// 最近のメッセージ履歴を取得
        /// </summary>
        Task<ChatMessage[]> GetRecentMessagesAsync(int count);

        /// <summary>
        /// ルームのメッセージ履歴を全て削除する
        /// </summary>
        Task DeleteRoomMessagesAsync();

        /// <summary>
        /// メッセージ受信イベント
        /// </summary>
        event Action<ChatMessage> OnMessageReceived;

        /// <summary>
        /// プレイヤー参加イベント
        /// </summary>
        event Action<string, string> OnPlayerJoined;

        /// <summary>
        /// プレイヤー退出イベント
        /// </summary>
        event Action<string, string> OnPlayerLeft;
    }
}
