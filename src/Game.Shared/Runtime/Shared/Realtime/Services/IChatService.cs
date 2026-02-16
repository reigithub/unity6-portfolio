using System.Threading.Tasks;
using Game.Library.Shared.Realtime.Dto;
using MagicOnion;

namespace Game.Library.Shared.Realtime.Services
{
    /// <summary>
    /// チャット Unary RPC サービスインターフェース
    /// </summary>
    public interface IChatService : IService<IChatService>
    {
        /// <summary>
        /// チャットルーム作成
        /// </summary>
        UnaryResult<CreateChatRoomResponse> CreateRoomAsync(CreateChatRoomRequest request);

        /// <summary>
        /// チャットルーム削除（Delete 権限が必要）
        /// </summary>
        UnaryResult<bool> DeleteRoomAsync(string roomId);

        /// <summary>
        /// メンバー招待（Invite 権限が必要）
        /// </summary>
        UnaryResult<bool> InviteMemberAsync(string roomId, string targetUserId, string playerName);

        /// <summary>
        /// メンバーキック（Kick 権限が必要）
        /// </summary>
        UnaryResult<bool> KickMemberAsync(string roomId, string targetUserId);

        /// <summary>
        /// メンバー権限変更（ManageMember 権限が必要）
        /// </summary>
        UnaryResult<bool> SetMemberPermissionsAsync(string roomId, string targetUserId, int permissions);

        /// <summary>
        /// チャットルーム情報取得
        /// </summary>
        UnaryResult<ChatRoomInfo> GetRoomInfoAsync(string roomId);

        /// <summary>
        /// チャットルームメンバー一覧取得
        /// </summary>
        UnaryResult<ChatRoomMemberInfo[]> GetRoomMembersAsync(string roomId);
    }
}
