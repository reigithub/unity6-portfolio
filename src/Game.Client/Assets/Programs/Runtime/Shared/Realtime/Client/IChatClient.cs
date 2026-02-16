using System;
using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// チャットクライアントインターフェース（Unary + Hub ベース）
    /// 1ユーザーが複数ルームに同時参加可能
    /// </summary>
    public interface IChatClient : IDisposable
    {
        // Unary（Hub 接続不要）

        /// <summary>
        /// チャットルーム作成
        /// </summary>
        Task<CreateChatRoomResponse> CreateRoomAsync(CreateChatRoomRequest request);

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
        Task<ChatRoomInfo> GetRoomInfoAsync(string roomId);

        /// <summary>
        /// チャットルームメンバー一覧取得
        /// </summary>
        Task<ChatRoomMemberInfo[]> GetRoomMembersAsync(string roomId);

        // Hub（roomId ごとに接続）

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
