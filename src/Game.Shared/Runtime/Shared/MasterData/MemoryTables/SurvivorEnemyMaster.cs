using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivor敵マスター
    /// 敵キャラクターの種類とパラメータを定義
    /// </summary>
    [MemoryTable("SurvivorEnemyMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorEnemyMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        /// <summary>敵タイプ（1:通常, 2:エリート, 3:ボス）</summary>
        [SecondaryKey(0), NonUnique]
        public int EnemyType { get; set; }

        /// <summary>基本HP</summary>
        public int BaseHp { get; set; }

        /// <summary>基本攻撃力</summary>
        public int BaseDamage { get; set; }

        /// <summary>移動速度</summary>
        public int MoveSpeed { get; set; }

        /// <summary>攻撃範囲</summary>
        public int AttackRange { get; set; }

        /// <summary>ドロップ経験値</summary>
        public int ExperienceValue { get; set; }

        /// <summary>ドロップアイテムID（null可）</summary>
        public int? DropItemId { get; set; }

        /// <summary>ドロップ確率（%）</summary>
        public int DropRate { get; set; }
    }
}
