using System.Collections.Generic;
using Game.Shared.Enums;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror 装備ショートカット（D-Pad 4スロット 1〜4）のセーブデータ。MemoryPack でバイナリ永続化する。
    /// 登録アイテムは (SlotType, Id) のみ保持し、静的属性（アイコン等）はマスターから引く。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorEquipmentShortcutSaveData
    {
        /// <summary>セーブデータバージョン（マイグレーション用）</summary>
        public int Version { get; set; } = 1;

        /// <summary>ショートカットスロット（index 0-3 ↔ 番号 1-4）。スロット数はサービスが 4 に整える。</summary>
        public List<HorrorEquipmentShortcutSlotData> Slots { get; set; } = new();
    }

    /// <summary>ショートカット1枠分の保存レコード。空スロットは <see cref="SlotType"/> が None。</summary>
    [MemoryPackable]
    public partial class HorrorEquipmentShortcutSlotData
    {
        public InventorySlotType SlotType { get; set; }

        public int Id { get; set; }
    }
}
