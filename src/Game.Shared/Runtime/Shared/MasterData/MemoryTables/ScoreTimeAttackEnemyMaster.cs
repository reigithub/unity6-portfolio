using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    [MemoryTable("ScoreTimeAttackEnemyMaster"), MessagePackObject(true)]
    public sealed partial class ScoreTimeAttackEnemyMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        public int WalkSpeed { get; set; }

        public int RunSpeed { get; set; }

        public int VisualDistance { get; set; }

        public int AuditoryDistance { get; set; }

        public int HpAttack { get; set; }
    }
}
