using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("EnemySpawnMaster"), MessagePackObject(true)]
    public sealed partial class EnemySpawnMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        [SecondaryKey(0), NonUnique]
        public int StageId { get; set; }

        public int GroupId { get; set; }

        public int EnemyId { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public int MinSpawnCount { get; set; }
        public int MaxSpawnCount { get; set; }
    }
}