using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror プレイヤー状態のセーブデータ。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorPlayerSaveData
    {
        /// <summary>未使用。MemoryPack のメンバー順序を保つための残置枠であり読み書きしない</summary>
        public int LastSavepointId { get; set; }

        /// <summary>残 HP（0 = 旧セーブ・未記録。ロード時に最大 HP へ正規化される）</summary>
        /// <remarks>MemoryPack は宣言順で直列化するため、後方互換のため末尾にのみ追加すること。</remarks>
        public int CurrentHealth { get; set; }
    }
}
