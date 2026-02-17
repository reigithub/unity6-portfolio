using System;
using System.Threading.Tasks;
using Game.Library.Shared.Chat.Dto;
using Game.Shared.Dto.Chat;

namespace Game.Shared.Chat.Client
{
    /// <summary>
    /// チャットクライアントインターフェース（REST + SignalR ベース）
    /// 1接続で複数ルームに同時参加可能
    /// </summary>
    public interface IChatClient : IDisposable
    {
        /// <summary>
        /// SignalR 接続設定（ConnectAsync の前に呼び出すこと）
        /// </summary>
        void Configure(string hubUrl, Func<Task<string>> accessTokenProvider);

        // REST 操作

        /// <summary>
        /// チャットルーム作成
        /// </summary>
        Task<CreateChatRoomRestResponse> CreateRoomAsync(CreateChatRoomRestRequest request);

        /// <summary>
        /// チャットルーム削除
        /// </summary>
        Task<bool> DeleteRoomAsync(string roomId);

        /// <summary>
        /// メンバー招待
        /// </summary>
        Task<bool> InviteMemberAsync(string roomId, string targetUserId, string playerName);

        /// <summary>
        /// メンバーキック
        /// </summary>
        Task<bool> KickMemberAsync(string roomId, string targetUserId);

        /// <summary>
        /// メンバー権限変更
        /// </summary>
        Task<bool> SetMemberPermissionsAsync(string roomId, string targetUserId, int permissions);

        /// <summary>
        /// チャットルーム情報取得
        /// </summary>
        Task<ChatRoomInfoResponse> GetRoomInfoAsync(string roomId);

        /// <summary>
        /// チャットルームメンバー一覧取得
        /// </summary>
        Task<ChatRoomMemberInfoResponse[]> GetRoomMembersAsync(string roomId);

        // SignalR 操作（1接続で複数ルーム）

        /// <summary>
        /// SignalR 接続を開始
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// チャットルームに参加
        /// </summary>
        Task JoinAsync(string roomId, string playerName);

        /// <summary>
        /// チャットルームから退出
        /// </summary>
        Task LeaveAsync(string roomId);

        /// <summary>
        /// メッセージ送信
        /// </summary>
        Task SendMessageAsync(string roomId, string content);

        /// <summary>
        /// 最近のメッセージ履歴を取得
        /// </summary>
        Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count);

        // イベント（roomId 付き）

        /// <summary>
        /// メッセージ受信イベント (roomId, message)
        /// </summary>
        event Action<string, ChatMessage> OnMessageReceived;

        /// <summary>
        /// プレイヤー参加イベント (roomId, userId, playerName)
        /// </summary>
        event Action<string, string, string> OnPlayerJoined;

        /// <summary>
        /// プレイヤー退出イベント (roomId, userId, playerName)
        /// </summary>
        event Action<string, string, string> OnPlayerLeft;

        /// <summary>
        /// ルーム削除通知イベント (roomId, reason)
        /// </summary>
        event Action<string, string> OnRoomDeleted;

        /// <summary>
        /// 権限変更通知イベント (roomId, permissions)
        /// </summary>
        event Action<string, int> OnPermissionsChanged;
    }
}
