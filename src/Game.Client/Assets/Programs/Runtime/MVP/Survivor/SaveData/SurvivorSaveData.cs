using System;
using System.Collections.Generic;
using MemoryPack;

namespace Game.MVP.Survivor.SaveData
{
    /// <summary>
    /// Survivorゲーム全体のセーブデータ
    /// </summary>
    [MemoryPackable]
    public partial class SurvivorSaveData
    {
        /// <summary>セーブデータバージョン（マイグレーション用）</summary>
        public int Version { get; set; } = 1;

        /// <summary>最終プレイ日時</summary>
        public DateTime LastPlayedAt { get; set; }

        /// <summary>総プレイ時間（秒）</summary>
        public float TotalPlayTime { get; set; }

        /// <summary>選択中のプレイヤーID</summary>
        public int SelectedPlayerId { get; set; } = 1;

        /// <summary>ステージクリア記録（StageId → Record）</summary>
        public Dictionary<int, StageClearRecord> StageRecords { get; set; } = new();

        /// <summary>アンロック済みステージID</summary>
        public HashSet<int> UnlockedStageIds { get; set; } = new() { 1 };

        /// <summary>現在のステージセッション（プレイ中/中断中、nullable）</summary>
        public StageSession CurrentSession { get; set; }
    }
}