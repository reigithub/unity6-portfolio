using System.Collections.Generic;
using Game.Shared.Enums;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror インベントリ（所持アイテム）のセーブデータ。MemoryPack でバイナリ永続化する。
    /// アイテムの静的属性はマスターから引くため、保存するのは ItemId と Count のみ。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorInventorySaveData
    {
        /// <summary>所持アイテム一覧（追加順）</summary>
        public List<HorrorInventorySlotData> Slots { get; set; } = new();
    }

    /// <summary>所持アイテム1種分の保存レコード。</summary>
    [MemoryPackable]
    public partial class HorrorInventorySlotData
    {
        public InventorySlotType SlotType { get; set; }

        public int Id { get; set; }

        public int Count { get; set; }
    }
}
