using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class ScoreSubmitDto
    {
        [Key(0)]
        [Required]
        public int StageId { get; set; }

        [Key(1)]
        [Required]
        public int Score { get; set; }

        [Key(2)]
        public float ClearTime { get; set; }

        [Key(3)]
        public int WaveReached { get; set; }

        [Key(4)]
        public int EnemiesDefeated { get; set; }
    }
}
