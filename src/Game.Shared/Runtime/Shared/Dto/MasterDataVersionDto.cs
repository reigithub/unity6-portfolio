using System.Collections.Generic;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class MasterDataVersionDto
    {
        public string Version { get; set; }

        public long UpdatedAt { get; set; }

        public Dictionary<string, string> TableHashes { get; set; }
    }
}
