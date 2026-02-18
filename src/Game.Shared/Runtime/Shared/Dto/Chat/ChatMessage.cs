using MessagePack;

namespace Game.Library.Shared.Dto
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
}
