using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror プレイヤー状態のセーブデータ。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorPlayerSaveData
    {
        /// <summary>最後に使ったセーブポイントの InteractionId（0 = 未記録）</summary>
        public int LastSavepointId { get; set; }
    }
}
