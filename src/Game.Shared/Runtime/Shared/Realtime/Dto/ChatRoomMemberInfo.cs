using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    /// <summary>
    /// チャットルームメンバー情報 DTO
    /// </summary>
    [MessagePackObject]
    public class ChatRoomMemberInfo
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string PlayerName { get; set; } = string.Empty;

        [Key(2)]
        public long JoinedAt { get; set; }

        [Key(3)]
        public int Permissions { get; set; }
    }
}
