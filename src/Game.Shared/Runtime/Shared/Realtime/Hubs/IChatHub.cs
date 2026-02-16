using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace Game.Library.Shared.Realtime.Hubs
{
    /// <summary>
    /// チャットメッセージ
    /// </summary>
    [MessagePackObject]
    public class ChatMessage
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string PlayerName { get; set; } = string.Empty;

        [Key(2)]
        public string Content { get; set; } = string.Empty;

        [Key(3)]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// チャットHub クライアント受信インターフェース
    /// </summary>
    public interface IChatHubReceiver
    {
        /// <summary>
        /// チャットメッセージ受信
        /// </summary>
        void OnMessageReceived(ChatMessage message);

        /// <summary>
        /// プレイヤーがチャットルームに参加した通知
        /// </summary>
        void OnPlayerJoined(string userId, string playerName);

        /// <summary>
        /// プレイヤーがチャットルームから退出した通知
        /// </summary>
        void OnPlayerLeft(string userId, string playerName);
    }

    /// <summary>
    /// チャットHub サーバー送信インターフェース（StreamingHub）
    /// </summary>
    public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
    {
        /// <summary>
        /// チャットルームに参加
        /// </summary>
        ValueTask JoinAsync(string roomId, string playerName);

        /// <summary>
        /// チャットルームから退出
        /// </summary>
        ValueTask LeaveAsync();

        /// <summary>
        /// メッセージ送信
        /// </summary>
        ValueTask SendMessageAsync(string content);

        /// <summary>
        /// 最近のメッセージ履歴を取得
        /// </summary>
        ValueTask<ChatMessage[]> GetRecentMessagesAsync(int count);

        /// <summary>
        /// ルームのメッセージ履歴を全て削除する
        /// </summary>
        ValueTask DeleteRoomMessagesAsync();
    }
}
