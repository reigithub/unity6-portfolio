using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Survivor.SaveData;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用セーブサービス
    /// CurrentSessionにサーバーセッション情報（stageId/playerId）を設定可能。
    /// 保存系メソッドはno-op。SurvivorServerSessionからStartSession()で情報を供給する。
    /// </summary>
    public class SurvivorServerSaveService : ISurvivorSaveService
    {
        private SurvivorSaveData _data = new();
        private SurvivorStageSession _currentSession;

        public SurvivorSaveData Data => _data;
        public bool IsLoaded => true;
        public bool IsDirty => false;
        public SurvivorStageSession CurrentSession => _currentSession;
        public bool HasActiveSession => _currentSession != null;

        public UniTask LoadAsync() => UniTask.CompletedTask;
        public UniTask SaveAsync() => UniTask.CompletedTask;
        public UniTask SaveIfDirtyAsync() => UniTask.CompletedTask;
        public UniTask DeleteAsync() => UniTask.CompletedTask;

        public void RecordStageClear(int stageId, int score, float clearTime, int kills, bool isVictory, bool isTimeUp = false, float hpRatio = 1f) { }
        public void UnlockStage(int stageId) { }
        public bool IsStageUnlocked(int stageId) => true;
        public SurvivorStageClearRecord GetStageRecord(int stageId) => null;
        public void DeleteStageRecord(int stageId) { }

        public void StartSession(int stageId, int playerId, int stageGroupId = 0)
        {
            _currentSession = new SurvivorStageSession
            {
                StageId = stageId,
                PlayerId = playerId,
                StageGroupId = stageGroupId,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void UpdateSession(int currentWave, float elapsedTime, int currentHp, int experience, int level, int score, int totalKills) { }

        public void CompleteCurrentStage(int score, int kills, float clearTime, bool isVictory, bool isTimeUp = false, float hpRatio = 1f) { }

        public void AdvanceToNextStage(int nextStageId) { }

        public void EndSession()
        {
            _currentSession = null;
        }

        public void SetSelectedPlayerId(int playerId)
        {
            _data.SelectedPlayerId = playerId;
        }

        public void AddPlayTime(float seconds) { }
    }
}
