using System.Collections.Generic;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class RankingResponse
    {
        public int StageId { get; set; }

        public int TotalCount { get; set; }

        public List<RankingEntryDto> Entries { get; set; } = new List<RankingEntryDto>();
    }
}
