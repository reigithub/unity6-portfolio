using System;
using System.Linq;
using Game.ScoreTimeAttack.Enums;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Core.Services;
using Game.ScoreTimeAttack.Data;
using Game.ScoreTimeAttack.Services;
using R3;
using UnityEngine;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackStageSceneModel
    {
        private MasterDataService _masterDataService;
        private MemoryDatabase MemoryDatabase => (_masterDataService ??= GameServiceManager.Get<MasterDataService>()).MemoryDatabase;

        public ScoreTimeAttackStageMaster StageMaster { get; private set; }
        public ScoreTimeAttackPlayerMaster PlayerMaster { get; private set; }

        public int? NextStageId { get; private set; }

        // Memo: データの持ち方は後日検討するとして、一旦動くものを作成
        public GameStageState StageState { get; set; }
        public GameStageResult StageResult { get; set; }

        public ReactiveProperty<int> CurrentTime { get; } = new();
        public int TotalTime { get; private set; }

        public ReactiveProperty<int> CurrentPoint { get; } = new();
        public int MaxPoint { get; private set; }

        public int PlayerCurrentHp { get; private set; }
        public int PlayerMaxHp { get; private set; }

        public ScoreTimeAttackStageSceneModel()
        {
            StageState = GameStageState.None;
            StageResult = GameStageResult.None;
        }

        public void Initialize(int stageId)
        {
            var stageMaster = MemoryDatabase.ScoreTimeAttackStageMasterTable.FindById(stageId);
            var playerMaster = MemoryDatabase.ScoreTimeAttackPlayerMasterTable.FindById(stageMaster.PlayerId ?? 1);
            StageMaster = stageMaster;
            PlayerMaster = playerMaster;

            CurrentTime.Value = stageMaster.TotalTime;
            TotalTime = stageMaster.TotalTime;
            CurrentPoint.Value = 0;
            MaxPoint = stageMaster.MaxPoint;

            PlayerCurrentHp = playerMaster.MaxHp;
            PlayerMaxHp = playerMaster.MaxHp;

            var stageMasters = MemoryDatabase.ScoreTimeAttackStageMasterTable.FindByGroupId(StageMaster.GroupId);
            bool isFirstStage = stageMasters.Min(x => x.Order) == stageMaster.Order;
            if (isFirstStage) GameServiceManager.Add<ScoreTimeAttackStageService>();

            NextStageId = stageMasters.OrderBy(x => x.Order).FirstOrDefault(x => x.Order > stageMaster.Order)?.Id;
        }

        public void ProgressTime()
        {
            CurrentTime.Value = Math.Max(0, CurrentTime.Value - 1);
        }

        public void AddPoint(int point)
        {
            CurrentPoint.Value = Mathf.Clamp(CurrentPoint.Value + point, 0, MaxPoint);
        }

        public void PlayerHpDamaged(int hpDamage)
        {
            PlayerCurrentHp = Mathf.Clamp(PlayerCurrentHp - hpDamage, 0, PlayerMaxHp);
        }

        public bool IsTimeUp()
        {
            return CurrentTime.Value <= 0;
        }

        public bool IsClear()
        {
            return CurrentPoint.Value >= MaxPoint;
        }

        public bool IsFailed()
        {
            return PlayerCurrentHp <= 0 || IsTimeUp();
        }

        public bool CanPause()
        {
            return StageState == GameStageState.Start;
        }

        public bool HasStageResult()
        {
            UpdateStageResult();
            return StageResult != GameStageResult.None;
        }

        public void UpdateStageResult()
        {
            if (IsClear())
            {
                StageResult = GameStageResult.Clear;
            }

            if (IsFailed())
            {
                StageResult = GameStageResult.Failed;
            }
        }

        public ScoreTimeAttackStageResultData CreateStageResult()
        {
            var result = new ScoreTimeAttackStageResultData
            {
                StageId = StageMaster.Id,
                StageResult = StageResult,
                CurrentTime = CurrentTime.Value,
                TotalTime = TotalTime,
                CurrentPoint = CurrentPoint.Value,
                MaxPoint = MaxPoint,
                PlayerCurrentHp = PlayerCurrentHp,
                PlayerMaxHp = PlayerMaxHp,
                NextStageId = NextStageId,
            };

            GameServiceManager.Get<ScoreTimeAttackStageService>().TryAddResult(result);

            return result;
        }
    }
}