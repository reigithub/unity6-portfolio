using System.Collections.Generic;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class RankingResponse
    {
        [Key(0)]
        public int StageId { get; set; }

        [Key(1)]
        public int TotalCount { get; set; }

        [Key(2)]
        public List<RankingEntryDto> Entries { get; set; } = new List<RankingEntryDto>();
    }
}
