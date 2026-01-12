using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    [MemoryTable("ScoreTimeAttackStageMaster"), MessagePackObject(true)]
    public sealed partial class ScoreTimeAttackStageMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        [SecondaryKey(0), NonUnique]
        public int GroupId { get; set; }

        public int Order { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public int TotalTime { get; set; }

        public int MaxPoint { get; set; }

        public int? PlayerId { get; set; }

        public int? NextStageId { get; set; }
    }
}
