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
        /// <summary>所持アイテム一覧（疎。位置は各行の SlotNo が持ち、List の並び順に意味はない）</summary>
        public List<HorrorInventorySlotData> Slots { get; set; } = new();
    }

    /// <summary>所持アイテム1スタック分の保存レコード。行が存在する = 中身のあるスタック（空位置は行なしで表現）。</summary>
    [MemoryPackable]
    public partial class HorrorInventorySlotData
    {
        public int SlotNo { get; set; }

        public ObjectCategory ObjectCategory { get; set; }

        public int Id { get; set; }

        public int Count { get; set; }
    }
}
