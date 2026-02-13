using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class RankingEntryDto
    {
        public int Rank { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int Score { get; set; }

        public float ClearTime { get; set; }

        public int StageId { get; set; }

        public long RecordedAt { get; set; }
    }
}
