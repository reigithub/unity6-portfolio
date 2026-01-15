using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorプレイヤーマスター
    /// プレイヤーキャラクターの基本パラメータを定義
    /// </summary>
    [MemoryTable("SurvivorPlayerMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorPlayerMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }

        public string AssetName { get; set; }

        /// <summary>最大HP</summary>
        public int MaxHp { get; set; }

        /// <summary>移動速度</summary>
        public int MoveSpeed { get; set; }

        /// <summary>アイテム拾得範囲</summary>
        public int PickupRange { get; set; }

        /// <summary>初期武器ID</summary>
        public int StartingWeaponId { get; set; }

        /// <summary>クリティカル率（%）</summary>
        public int CriticalRate { get; set; }

        /// <summary>クリティカルダメージ倍率（%）</summary>
        public int CriticalDamage { get; set; }
    }
}
