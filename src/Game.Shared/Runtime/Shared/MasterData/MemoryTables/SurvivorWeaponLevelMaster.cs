using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivor武器レベルマスター
    /// 武器のレベルごとのパラメータを定義
    /// </summary>
    [MemoryTable("SurvivorWeaponLevelMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorWeaponLevelMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>武器ID</summary>
        [SecondaryKey(0), NonUnique]
        public int WeaponId { get; set; }

        /// <summary>レベル</summary>
        public int Level { get; set; }

        /// <summary>ダメージ</summary>
        public int Damage { get; set; }

        /// <summary>クールダウン（ミリ秒）</summary>
        public int Cooldown { get; set; }

        /// <summary>射程/範囲</summary>
        public int Range { get; set; }

        /// <summary>弾数/ヒット数</summary>
        public int ProjectileCount { get; set; }

        /// <summary>弾速</summary>
        public int ProjectileSpeed { get; set; }

        /// <summary>貫通数（0=貫通なし）</summary>
        public int Pierce { get; set; }

        /// <summary>レベルアップ時の説明</summary>
        public string LevelUpDescription { get; set; }
    }
}
