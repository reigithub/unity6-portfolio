using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class SurvivorScoreSubmitResponse
    {
        public long ScoreId { get; set; }

        public bool IsNewBest { get; set; }

        public int CurrentRank { get; set; }
    }

    [MessagePackObject(true)]
    public class SurvivorScoreHistoryEntry
    {
        public long Id { get; set; }

        public int StageId { get; set; }

        public int Score { get; set; }

        public float ClearTime { get; set; }

        public int WaveReached { get; set; }

        public int EnemiesDefeated { get; set; }

        public long RecordedAt { get; set; }
    }
}
