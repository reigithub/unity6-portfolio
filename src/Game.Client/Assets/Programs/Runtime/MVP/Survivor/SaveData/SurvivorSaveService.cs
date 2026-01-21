using System;
using System.Collections.Generic;
using Game.Shared.SaveData;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.SaveData
{
    /// <summary>
    /// Survivorセーブデータサービス実装
    /// SaveServiceBaseを継承し、Survivor固有のセーブ機能を提供
    /// </summary>
    public class SurvivorSaveService : SaveServiceBase<SurvivorSaveData>, ISurvivorSaveService
    {
        private const int DataVersion = 1;

        [Inject] private readonly IMasterDataService _masterDataService;

        protected override string SaveKey => "survivor_save";
        protected override int CurrentVersion => DataVersion;

        public SurvivorStageSession CurrentSession => Data?.CurrentSession;
        public bool HasActiveSession => Data?.CurrentSession != null && !Data.CurrentSession.IsCompleted;

        public SurvivorSaveService(ISaveDataStorage storage) : base(storage)
        {
        }

        #region ステージ記録

        public void RecordStageClear(int stageId, int score, float clearTime, int kills, bool isVictory, bool isTimeUp = false, float hpRatio = 1f)
        {
            if (Data == null) return;

            if (!Data.StageRecords.TryGetValue(stageId, out var record))
            {
                record = new SurvivorStageClearRecord { StageId = stageId };
                Data.StageRecords[stageId] = record;
            }

            record.LastPlayedAt = DateTime.Now;

            if (isVictory)
            {
                if (!record.IsCleared)
                {
                    record.IsCleared = true;
                    record.FirstClearedAt = DateTime.Now;
                }

                record.ClearCount++;
                record.HighScore = Math.Max(record.HighScore, score);
                record.BestClearTime = Math.Min(record.BestClearTime, clearTime);
                record.MaxKills = Math.Max(record.MaxKills, kills);
                record.StarRating = Math.Max(record.StarRating, CalculateStarRating(isTimeUp, hpRatio));

                // 次ステージアンロック
                UnlockNextStage(stageId);
            }

            MarkDirty();

            Debug.Log($"[SurvivorSaveService] Stage {stageId} record updated. " +
                      $"Victory: {isVictory}, Score: {score}, Stars: {record.StarRating}, TimeUp: {isTimeUp}, HP: {hpRatio:P0}");
        }

        public void UnlockStage(int stageId)
        {
            if (Data == null) return;

            if (Data.UnlockedStageIds.Add(stageId))
            {
                MarkDirty();
                Debug.Log($"[SurvivorSaveService] Stage {stageId} unlocked!");
            }
        }

        public bool IsStageUnlocked(int stageId)
        {
            return Data?.UnlockedStageIds.Contains(stageId) ?? false;
        }

        public SurvivorStageClearRecord GetStageRecord(int stageId)
        {
            if (Data == null) return null;
            return Data.StageRecords.GetValueOrDefault(stageId);
        }

        #endregion

        #region セッション管理

        public void StartSession(int stageId, int playerId, int stageGroupId = 0)
        {
            if (Data == null) return;

            Data.CurrentSession = new SurvivorStageSession
            {
                StageGroupId = stageGroupId,
                CurrentStageIndex = 0,
                StageId = stageId,
                PlayerId = playerId,
                CurrentWave = 1,
                ElapsedTime = 0f,
                CurrentHp = 0, // 初期化時に設定される
                Experience = 0,
                Level = 1,
                Score = 0,
                TotalKills = 0,
                EquippedWeaponIds = new List<int>(),
                StageResults = new List<SurvivorStageResultData>(),
                StartedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsCompleted = false
            };

            MarkDirty();

            Debug.Log($"[SurvivorSaveService] Session started. Stage: {stageId}, Player: {playerId}, Group: {stageGroupId}");
        }

        public void UpdateSession(
            int currentWave,
            float elapsedTime,
            int currentHp,
            int experience,
            int level,
            int score,
            int totalKills)
        {
            if (Data?.CurrentSession == null || Data.CurrentSession.IsCompleted) return;

            var session = Data.CurrentSession;
            session.CurrentWave = currentWave;
            session.ElapsedTime = elapsedTime;
            session.CurrentHp = currentHp;
            session.Experience = experience;
            session.Level = level;
            session.Score = score;
            session.TotalKills = totalKills;
            session.UpdatedAt = DateTime.Now;

            MarkDirty();

            Debug.Log($"[SurvivorSaveService] Session updated. Wave: {currentWave}, Time: {elapsedTime:F1}s");
        }

        public void CompleteCurrentStage(int score, int kills, float clearTime, bool isVictory, bool isTimeUp = false, float hpRatio = 1f)
        {
            if (Data?.CurrentSession == null) return;

            var session = Data.CurrentSession;

            // 結果を記録
            session.StageResults.Add(new SurvivorStageResultData
            {
                StageId = session.StageId,
                Score = score,
                Kills = kills,
                ClearTime = clearTime,
                HpRatio = hpRatio,
                IsVictory = isVictory,
                CompletedAt = DateTime.Now
            });

            session.IsCompleted = true;
            session.UpdatedAt = DateTime.Now;

            // 永続記録にも保存
            RecordStageClear(session.StageId, score, clearTime, kills, isVictory, isTimeUp, hpRatio);

            Debug.Log($"[SurvivorSaveService] Stage completed. Victory: {isVictory}, Score: {score}, TimeUp: {isTimeUp}, HP: {hpRatio:P0}");
        }

        public void AdvanceToNextStage(int nextStageId)
        {
            if (Data?.CurrentSession == null) return;

            var session = Data.CurrentSession;
            session.CurrentStageIndex++;
            session.StageId = nextStageId;
            session.CurrentWave = 1;
            session.ElapsedTime = 0f;
            session.Experience = 0;
            session.IsCompleted = false;
            session.UpdatedAt = DateTime.Now;

            MarkDirty();

            Debug.Log($"[SurvivorSaveService] Advanced to next stage. StageId: {nextStageId}, Index: {session.CurrentStageIndex}");
        }

        public void EndSession()
        {
            if (Data == null) return;

            if (Data.CurrentSession != null)
            {
                Debug.Log($"[SurvivorSaveService] Session ended. " +
                          $"Total stages: {Data.CurrentSession.StageResults.Count}, " +
                          $"Total score: {Data.CurrentSession.TotalGroupScore}");

                Data.CurrentSession = null;
                MarkDirty();
            }
        }

        #endregion

        #region プレイヤー設定

        public void SetSelectedPlayerId(int playerId)
        {
            if (Data == null) return;

            if (Data.SelectedPlayerId != playerId)
            {
                Data.SelectedPlayerId = playerId;
                MarkDirty();
            }
        }

        public void AddPlayTime(float seconds)
        {
            if (Data == null) return;

            Data.TotalPlayTime += seconds;
            MarkDirty();
        }

        #endregion

        #region SaveServiceBase Overrides

        protected override SurvivorSaveData CreateNewData()
        {
            return new SurvivorSaveData
            {
                Version = CurrentVersion,
                LastPlayedAt = DateTime.Now,
                SelectedPlayerId = 1,
                UnlockedStageIds = new System.Collections.Generic.HashSet<int> { 1 }
            };
        }

        protected override int GetDataVersion(SurvivorSaveData data)
        {
            return data.Version;
        }

        protected override void MigrateData(SurvivorSaveData data, int fromVersion)
        {
            // バージョン1からのマイグレーション
            // if (fromVersion < 2) { ... }

            data.Version = CurrentVersion;
            Debug.Log($"[SurvivorSaveService] Data migrated from version {fromVersion} to {CurrentVersion}");
        }

        protected override void OnBeforeSave(SurvivorSaveData data)
        {
            data.LastPlayedAt = DateTime.Now;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 星評価を計算
        /// ★☆☆ (1星): 時間切れでクリア（全Wave未クリア）
        /// ★★☆ (2星): 全Waveクリア（ボス撃破含む）
        /// ★★★ (3星): 全Waveクリア + 残りHP50%以上
        /// </summary>
        private int CalculateStarRating(bool isTimeUp, float hpRatio)
        {
            // 時間切れクリアの場合は強制的に1星
            if (isTimeUp)
            {
                Debug.Log($"[SurvivorSaveService] TimeUp clear - 1 star");
                return 1;
            }

            // 全Waveクリア + HP50%以上 → 3星
            if (hpRatio >= 0.5f)
            {
                Debug.Log($"[SurvivorSaveService] All waves clear with HP {hpRatio:P0} - 3 stars");
                return 3;
            }

            // 全Waveクリア → 2星
            Debug.Log($"[SurvivorSaveService] All waves clear with HP {hpRatio:P0} - 2 stars");
            return 2;
        }

        private void UnlockNextStage(int clearedStageId)
        {
            if (_masterDataService?.MemoryDatabase == null) return;

            var stageTable = _masterDataService.MemoryDatabase.SurvivorStageMasterTable;

            // このステージをクリアすることで解放されるステージを探す
            foreach (var stage in stageTable.All)
            {
                if (stage.UnlockStageId == clearedStageId)
                {
                    UnlockStage(stage.Id);
                }
            }
        }

        #endregion
    }
}