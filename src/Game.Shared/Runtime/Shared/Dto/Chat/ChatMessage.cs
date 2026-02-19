using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// チャットメッセージ
    /// </summary>
    [MessagePackObject]
    public class ChatMessage
    {
        [Key(0)]
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string PlayerName { get; set; } = string.Empty;

        [Key(2)]
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        [Key(3)]
        public long Timestamp { get; set; }
    }
}
