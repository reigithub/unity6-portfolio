using System;
using System.Collections.Generic;
using MemoryPack;

namespace Game.ScoreTimeAttack.SaveData
{
    /// <summary>
    /// タイムアタックゲームのセーブデータ
    /// </summary>
    [MemoryPackable]
    public partial class TimeAttackSaveData
    {
        /// <summary>セーブデータバージョン</summary>
        public int Version { get; set; } = 1;

        /// <summary>最終プレイ日時</summary>
        public DateTime LastPlayedAt { get; set; }

        /// <summary>総プレイ回数</summary>
        public int TotalPlayCount { get; set; }

        /// <summary>ステージ別ベストタイム（StageId → BestTime秒）</summary>
        public Dictionary<int, float> BestTimes { get; set; } = new();

        /// <summary>ステージ別ベストスコア（StageId → Score）</summary>
        public Dictionary<int, int> BestScores { get; set; } = new();

        /// <summary>アンロック済みキャラクターID</summary>
        public HashSet<int> UnlockedCharacterIds { get; set; } = new() { 1 };

        /// <summary>選択中のキャラクターID</summary>
        public int SelectedCharacterId { get; set; } = 1;
    }
}
