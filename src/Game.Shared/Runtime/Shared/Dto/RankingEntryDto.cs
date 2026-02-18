using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class RankingEntryDto
    {
        [Key(0)]
        public int Rank { get; set; }

        [Key(1)]
        public string UserId { get; set; } = string.Empty;

        [Key(2)]
        public string UserName { get; set; } = string.Empty;

        [Key(3)]
        public int Score { get; set; }

        [Key(4)]
        public float ClearTime { get; set; }

        [Key(5)]
        public int StageId { get; set; }

        [Key(6)]
        public long RecordedAt { get; set; }
    }
}
