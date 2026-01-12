using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    [MemoryTable("ScoreTimeAttackStageItemMaster"), MessagePackObject(true)]
    public sealed partial class ScoreTimeAttackStageItemMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        [SecondaryKey(0)]
        public string AssetName { get; set; }

        public int Point { get; set; }
    }
}
