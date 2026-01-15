using System;
using Game.Shared.SaveData;
using UnityEngine;

namespace Game.ScoreTimeAttack.SaveData
{
    /// <summary>
    /// タイムアタックゲームのセーブサービス
    /// SaveServiceBaseを継承した実装例
    /// </summary>
    public class TimeAttackSaveService : SaveServiceBase<TimeAttackSaveData>
    {
        protected override string SaveKey => "timeattack_save";
        protected override int CurrentVersion => 1;

        public TimeAttackSaveService(ISaveDataStorage storage) : base(storage)
        {
        }

        /// <summary>
        /// ベストタイムを記録
        /// </summary>
        public void RecordBestTime(int stageId, float time)
        {
            if (Data == null) return;

            if (!Data.BestTimes.TryGetValue(stageId, out var currentBest) || time < currentBest)
            {
                Data.BestTimes[stageId] = time;
                MarkDirty();
                Debug.Log($"[TimeAttackSaveService] New best time for stage {stageId}: {time:F2}s");
            }
        }

        /// <summary>
        /// ベストスコアを記録
        /// </summary>
        public void RecordBestScore(int stageId, int score)
        {
            if (Data == null) return;

            if (!Data.BestScores.TryGetValue(stageId, out var currentBest) || score > currentBest)
            {
                Data.BestScores[stageId] = score;
                MarkDirty();
                Debug.Log($"[TimeAttackSaveService] New best score for stage {stageId}: {score}");
            }
        }

        /// <summary>
        /// プレイ回数を加算
        /// </summary>
        public void IncrementPlayCount()
        {
            if (Data == null) return;

            Data.TotalPlayCount++;
            MarkDirty();
        }

        /// <summary>
        /// キャラクターをアンロック
        /// </summary>
        public void UnlockCharacter(int characterId)
        {
            if (Data == null) return;

            if (Data.UnlockedCharacterIds.Add(characterId))
            {
                MarkDirty();
                Debug.Log($"[TimeAttackSaveService] Character {characterId} unlocked!");
            }
        }

        /// <summary>
        /// ベストタイムを取得
        /// </summary>
        public float? GetBestTime(int stageId)
        {
            if (Data == null) return null;
            return Data.BestTimes.TryGetValue(stageId, out var time) ? time : null;
        }

        /// <summary>
        /// ベストスコアを取得
        /// </summary>
        public int? GetBestScore(int stageId)
        {
            if (Data == null) return null;
            return Data.BestScores.TryGetValue(stageId, out var score) ? score : null;
        }

        protected override TimeAttackSaveData CreateNewData()
        {
            return new TimeAttackSaveData
            {
                Version = CurrentVersion,
                LastPlayedAt = DateTime.Now,
                UnlockedCharacterIds = new System.Collections.Generic.HashSet<int> { 1 },
                SelectedCharacterId = 1
            };
        }

        protected override int GetDataVersion(TimeAttackSaveData data)
        {
            return data.Version;
        }

        protected override void OnBeforeSave(TimeAttackSaveData data)
        {
            data.LastPlayedAt = DateTime.Now;
        }
    }
}
