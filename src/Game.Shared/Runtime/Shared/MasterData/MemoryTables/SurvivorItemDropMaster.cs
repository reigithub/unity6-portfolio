using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorアイテムドロップマスター
    /// ドロップグループごとにアイテムとドロップ確率を定義
    /// </summary>
    [MemoryTable("SurvivorItemDropMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorItemDropMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>ドロップグループID（SurvivorStageWaveEnemyMasterから参照）</summary>
        [SecondaryKey(0), NonUnique]
        public int GroupId { get; set; }

        /// <summary>ドロップアイテムID（SurvivorItemMaster参照）</summary>
        public int ItemId { get; set; }

        /// <summary>ドロップ確率（万分率、グループ内での相対確率）</summary>
        public int DropRate { get; set; }
    }
}
