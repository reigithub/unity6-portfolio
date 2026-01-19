using System;
using System.Collections.Generic;
using MemoryPack;

namespace Game.MVP.Survivor.SaveData
{
    /// <summary>
    /// ステージセッション
    /// プレイ中の状態管理、中断復帰、リザルト表示に使用
    /// </summary>
    [MemoryPackable]
    public partial class SurvivorStageSession
    {
        #region グループ情報（将来的な連続ステージ用）

        /// <summary>ステージグループID（マスタのGroupId、0 = 単発ステージ）</summary>
        public int StageGroupId { get; set; }

        /// <summary>グループ内の進行位置（0始まり）</summary>
        public int CurrentStageIndex { get; set; }

        #endregion

        #region 現在ステージ情報

        /// <summary>ステージID</summary>
        public int StageId { get; set; }

        /// <summary>プレイヤーID</summary>
        public int PlayerId { get; set; }

        #endregion

        #region プレイ状態

        /// <summary>現在のウェーブ</summary>
        public int CurrentWave { get; set; }

        /// <summary>経過時間（秒）</summary>
        public float ElapsedTime { get; set; }

        /// <summary>現在HP</summary>
        public int CurrentHp { get; set; }

        /// <summary>経験値</summary>
        public int Experience { get; set; }

        /// <summary>レベル</summary>
        public int Level { get; set; }

        /// <summary>スコア</summary>
        public int Score { get; set; }

        /// <summary>総キル数</summary>
        public int TotalKills { get; set; }

        /// <summary>装備中の武器ID</summary>
        public List<int> EquippedWeaponIds { get; set; } = new();

        #endregion

        #region グループ内結果履歴

        /// <summary>グループ内のステージ結果リスト（リザルト画面用）</summary>
        public List<SurvivorStageResultData> StageResults { get; set; } = new();

        #endregion

        #region メタ情報

        /// <summary>セッション開始日時</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>最終更新日時</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>完了済み（クリア/ゲームオーバー）</summary>
        public bool IsCompleted { get; set; }

        #endregion

        #region 集計プロパティ

        /// <summary>グループ内の合計スコア</summary>
        [MemoryPackIgnore]
        public int TotalGroupScore
        {
            get
            {
                int total = 0;
                foreach (var result in StageResults)
                    total += result.Score;
                return total;
            }
        }

        /// <summary>グループ内の合計キル数</summary>
        [MemoryPackIgnore]
        public int TotalGroupKills
        {
            get
            {
                int total = 0;
                foreach (var result in StageResults)
                    total += result.Kills;
                return total;
            }
        }

        #endregion
    }

    /// <summary>
    /// ステージ結果データ（セッション内の各ステージ結果）
    /// </summary>
    [MemoryPackable]
    public partial class SurvivorStageResultData
    {
        /// <summary>ステージID</summary>
        public int StageId { get; set; }

        /// <summary>スコア</summary>
        public int Score { get; set; }

        /// <summary>キル数</summary>
        public int Kills { get; set; }

        /// <summary>クリア時間（秒）</summary>
        public float ClearTime { get; set; }

        /// <summary>勝利したか</summary>
        public bool IsVictory { get; set; }

        /// <summary>完了日時</summary>
        public DateTime CompletedAt { get; set; }
    }
}
