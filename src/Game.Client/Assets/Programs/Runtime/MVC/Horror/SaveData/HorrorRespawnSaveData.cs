using MemoryPack;

namespace Game.Horror.SaveData
{
    [MemoryPackable]
    public partial class HorrorRespawnSaveData
    {
        public int Version { get; set; } = 1;

        /// <summary>最後に使ったセーブポイントの InteractionId（0 = 未記録）</summary>
        public int LastSavepointId { get; set; }
    }
}
