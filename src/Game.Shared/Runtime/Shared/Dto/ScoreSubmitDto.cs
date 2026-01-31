using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class ScoreSubmitDto
    {
        public int StageId { get; set; }

        public int Score { get; set; }

        public float ClearTime { get; set; }

        public string GameMode { get; set; }

        public int WaveReached { get; set; }

        public int EnemiesDefeated { get; set; }
    }
}
