using System.Collections.Generic;
using Game.Shared.Enums;
using MemoryPack;

namespace Game.Horror.SaveData
{
    [MemoryPackable]
    public partial class HorrorKeyItemSaveData
    {
        public List<HorrorKeyItemData> KeyItems { get; set; } = new();
    }

    [MemoryPackable]
    public partial class HorrorKeyItemData
    {
        public ObjectCategory ObjectCategory { get; set; }
        public int Id { get; set; }
    }
}
