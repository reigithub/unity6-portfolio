using System.Collections.Generic;
using MemoryPack;

namespace Game.Horror.SaveData
{
    [MemoryPackable]
    public partial class HorrorInteractionSaveData
    {
        public List<int> InteractionIds { get; set; } = new();
    }
}
