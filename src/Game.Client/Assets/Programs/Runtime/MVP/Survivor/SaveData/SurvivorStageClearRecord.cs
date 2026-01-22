using System;
using MemoryPack;

namespace Game.MVP.Survivor.SaveData
{
    /// <summary>
    /// ステージごとのクリア記録
    /// </summary>
    [MemoryPackable]
    public partial class SurvivorStageClearRecord
    {
        /// <summary>ステージID</summary>
        public int StageId { get; set; }

        /// <summary>クリア済みか</summary>
        public bool IsCleared { get; set; }

        /// <summary>クリア回数</summary>
        public int ClearCount { get; set; }

        /// <summary>ベストスコア</summary>
        public int HighScore { get; set; }

        /// <summary>
        /// 最短クリアタイム（秒）
        /// 0以下は未記録を意味する（MemoryPackデシリアライズでデフォルト値が無視されるため）
        /// </summary>
        public float BestClearTime { get; set; }

        /// <summary>BestClearTimeが有効（記録済み）かどうか</summary>
        [MemoryPackIgnore]
        public bool HasBestClearTime => BestClearTime > 0;

        /// <summary>最大キル数</summary>
        public int MaxKills { get; set; }

        /// <summary>星評価（1-3）</summary>
        public int StarRating { get; set; }

        /// <summary>初回クリア日時</summary>
        public DateTime? FirstClearedAt { get; set; }

        /// <summary>最終プレイ日時</summary>
        public DateTime LastPlayedAt { get; set; }
    }
}
