using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    /// <summary>
    /// チャットルーム作成レスポンス DTO
    /// </summary>
    [MessagePackObject]
    public class CreateChatRoomResponse
    {
        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public string RoomId { get; set; } = string.Empty;

        [Key(2)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
