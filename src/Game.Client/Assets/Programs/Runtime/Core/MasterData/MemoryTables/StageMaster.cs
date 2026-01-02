using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("StageMaster"), MessagePackObject(true)]
    public sealed partial class StageMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public int TotalTime { get; set; }

        public int MaxPoint { get; set; }

        public int? PlayerId { get; set; }

        public int? NextStageId { get; set; }
    }
}