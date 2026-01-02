using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("StageItemMaster"), MessagePackObject(true)]
    public sealed partial class StageItemMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public int Point { get; set; }
    }
}