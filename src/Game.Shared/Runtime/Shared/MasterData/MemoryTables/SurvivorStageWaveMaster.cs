using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorステージウェーブマスター
    /// ウェーブの基本設定を定義
    /// </summary>
    [MemoryTable("SurvivorStageWaveMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorStageWaveMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>ステージID</summary>
        [SecondaryKey(0), NonUnique]
        public int StageId { get; set; }

        /// <summary>ウェーブ番号</summary>
        public int WaveNumber { get; set; }

        /// <summary>ウェーブ開始時間（秒）</summary>
        public int StartTime { get; set; }

        /// <summary>ウェーブ継続時間（秒）</summary>
        public int Duration { get; set; }

        /// <summary>敵速度倍率（%）</summary>
        public int EnemySpeedMultiplier { get; set; }

        /// <summary>敵HP倍率（%）</summary>
        public int EnemyHealthMultiplier { get; set; }

        /// <summary>敵攻撃力倍率（%）</summary>
        public int EnemyDamageMultiplier { get; set; }

        /// <summary>経験値倍率（%）</summary>
        public int ExperienceMultiplier { get; set; }
    }
}
