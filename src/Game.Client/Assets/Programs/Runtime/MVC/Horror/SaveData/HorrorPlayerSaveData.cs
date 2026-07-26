using Game.Horror.Constants;
using MemoryPack;

namespace Game.Horror.SaveData
{
    /// <summary>
    /// Horror プレイヤー状態のセーブデータ。
    /// </summary>
    [MemoryPackable]
    public partial class HorrorPlayerSaveData
    {
        /// <summary>操作するプレイヤーの Id（HorrorPlayerMaster の Id）。マスター不在の値は参照側が既定 Id へフォールバックする</summary>
        public int PlayerId { get; set; } = HorrorSaveConstants.DefaultPlayerId;

        /// <summary>残 HP（0 = 旧セーブ・未記録。ロード時に最大 HP へ正規化される）</summary>
        /// <remarks>MemoryPack は宣言順で直列化するため、後方互換のため末尾にのみ追加すること。</remarks>
        public int CurrentHealth { get; set; }
    }
}
