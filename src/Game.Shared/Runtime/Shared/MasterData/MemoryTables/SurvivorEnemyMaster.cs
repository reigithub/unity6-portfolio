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

        /// <summary>移動速度（1000倍値、ToUnit()で変換）</summary>
        public int MoveSpeed { get; set; }

        /// <summary>攻撃範囲（1000倍値、ToUnit()で変換）</summary>
        public int AttackRange { get; set; }

        /// <summary>攻撃クールダウン（ms、ToSeconds()で変換）</summary>
        public int AttackCooldown { get; set; }

        /// <summary>ヒットスタン時間（ms、ToSeconds()で変換）</summary>
        public int HitStunDuration { get; set; }

        /// <summary>回転速度（度/s）</summary>
        public int RotationSpeed { get; set; }

        /// <summary>死亡アニメーション時間（ms、ToSeconds()で変換）</summary>
        public int DeathAnimDuration { get; set; }

        /// <summary>ドロップ経験値</summary>
        public int ExperienceValue { get; set; }

        /// <summary>ドロップアイテムID（null可）</summary>
        public int? DropItemId { get; set; }

        /// <summary>ドロップ確率（万分率、ToRate()で変換）</summary>
        public int DropRate { get; set; }

        /// <summary>同時存在上限（0=無制限）</summary>
        public int MaxConcurrent { get; set; }

        /// <summary>スポーン時の衝突判定半径（1000倍値、ToUnit()で変換）</summary>
        public int SpawnRadius { get; set; }
    }
}
