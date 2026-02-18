using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// チャットルーム情報 DTO
    /// </summary>
    [MessagePackObject]
    public class ChatRoomInfo
    {
        [Key(0)]
        public string RoomId { get; set; } = string.Empty;

        [Key(1)]
        public string RoomName { get; set; } = string.Empty;

        [Key(2)]
        public string RoomType { get; set; } = string.Empty;

        [Key(3)]
        public int CurrentMembers { get; set; }

        [Key(4)]
        public int MaxMembers { get; set; }

        [Key(5)]
        public long CreatedAt { get; set; }

        [Key(6)]
        public int DefaultPermissions { get; set; }
    }
}
