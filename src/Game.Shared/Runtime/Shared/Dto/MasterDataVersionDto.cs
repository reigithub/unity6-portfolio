using System.Collections.Generic;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class MasterDataVersionDto
    {
        [Key(0)]
        public string Version { get; set; }

        [Key(1)]
        public long UpdatedAt { get; set; }

        [Key(2)]
        public Dictionary<string, string> TableHashes { get; set; }
    }
}
