using Game.Core.Enums;
using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("AudioMaster"), MessagePackObject(true)]
    public sealed partial class AudioMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public string Desc { get; set; }

        [SecondaryKey(0), NonUnique]
        public int AudioCategory { get; set; }
    }
}