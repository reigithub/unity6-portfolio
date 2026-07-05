using Game.Shared.Enums;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror 装備状態のセーブデータ。MemoryPack でバイナリ永続化する。
    /// 装備中の (SlotType, Id) のみ保持し、静的属性（ダメージ等）はマスターから引く。未装備は <see cref="SlotType"/> が None。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorEquipmentSaveData
    {
        /// <summary>セーブデータバージョン（マイグレーション用）</summary>
        public int Version { get; set; } = 1;

        /// <summary>装備中のスロット種別。未装備は None。</summary>
        public InventorySlotType SlotType { get; set; }

        /// <summary>装備中の Id（マスター PrimaryKey）。未装備時は無効（0）。</summary>
        public int Id { get; set; }
    }
}
