using System.ComponentModel.DataAnnotations;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class ScoreSubmitDto
    {
        [Required]
        public int StageId { get; set; }

        [Required]
        public int Score { get; set; }

        public float ClearTime { get; set; }

        public int WaveReached { get; set; }

        public int EnemiesDefeated { get; set; }
    }
}
