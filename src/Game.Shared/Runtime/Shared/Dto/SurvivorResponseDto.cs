using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class SurvivorScoreSubmitResponse
    {
        [Key(0)]
        public long ScoreId { get; set; }

        [Key(1)]
        public bool IsNewBest { get; set; }

        [Key(2)]
        public int CurrentRank { get; set; }
    }

    [MessagePackObject]
    public class SurvivorScoreHistoryEntry
    {
        [Key(0)]
        public long Id { get; set; }

        [Key(1)]
        public int StageId { get; set; }

        [Key(2)]
        public int Score { get; set; }

        [Key(3)]
        public float ClearTime { get; set; }

        [Key(4)]
        public int WaveReached { get; set; }

        [Key(5)]
        public int EnemiesDefeated { get; set; }

        [Key(6)]
        public long RecordedAt { get; set; }
    }
}
