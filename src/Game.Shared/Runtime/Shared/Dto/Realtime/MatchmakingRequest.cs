using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class MatchmakingRequest
    {
        [Key(0)]
        [Required]
        [StringLength(30, MinimumLength = 1)]
        public string GameMode { get; set; } = string.Empty;

        [Key(1)]
        public int StageId { get; set; }

        [Key(2)]
        [Range(2, 16)]
        public int MatchSize { get; set; } = 2;
    }
}
