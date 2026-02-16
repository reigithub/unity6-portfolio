using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    /// <summary>
    /// チャットルーム作成リクエスト DTO
    /// </summary>
    [MessagePackObject]
    public class CreateChatRoomRequest
    {
        [Key(0)]
        public string RoomName { get; set; } = string.Empty;

        [Key(1)]
        public string RoomType { get; set; } = string.Empty;

        [Key(2)]
        public int MaxMembers { get; set; }

        [Key(3)]
        public int DefaultPermissions { get; set; }

        [Key(4)]
        public int CreatorPermissions { get; set; }
    }
}
