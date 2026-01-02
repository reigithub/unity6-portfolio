using System;
using Game.Core.MasterData;
using Game.Core.MasterData.MemoryTables;
using Game.Core.Services;
using R3;
using UnityEngine;

namespace Game.Contents.Scenes
{
    public enum GameStageState
    {
        None,
        Ready,
        Start,
        Retry,
        Result,
        Finish
    }

    public enum GameStageResult
    {
        None,
        Clear,
        Failed,
    }

    public struct GameStageResultData
    {
        public GameStageResult StageResult { get; set; }

        public int CurrentTime { get; set; }
        public int TotalTime { get; set; }

        public int CurrentPoint { get; set; }
        public int MaxPoint { get; set; }

        public int PlayerCurrentHp { get; set; }
        public int PlayerMaxHp { get; set; }

        public int? NextStageId { get; set; }
    }

    public class GameStageSceneModel
    {
        private GameServiceReference<MasterDataService> _masterDataService;
        protected MemoryDatabase MemoryDatabase => _masterDataService.Reference.MemoryDatabase;

        public StageMaster StageMaster { get; private set; }
        public PlayerMaster PlayerMaster { get; private set; }

        // Memo: データの持ち方は後日検討するとして、一旦動くものを作成
        public GameStageState StageState { get; set; }
        public GameStageResult StageResult { get; set; }

        public ReactiveProperty<int> CurrentTime { get; set; } = new();
        public int TotalTime { get; set; }

        public ReactiveProperty<int> CurrentPoint { get; set; } = new();
        public int MaxPoint { get; set; }

        public int PlayerCurrentHp { get; set; }
        public int PlayerMaxHp { get; set; }

        public GameStageSceneModel()
        {
            StageState = GameStageState.None;
            StageResult = GameStageResult.None;
        }

        public void Initialize(int stageId)
        {
            var stageMaster = MemoryDatabase.StageMasterTable.FindById(stageId);
            var playerMaster = MemoryDatabase.PlayerMasterTable.FindById(stageMaster.PlayerId ?? 1);
            StageMaster = stageMaster;
            PlayerMaster = playerMaster;

            CurrentTime.Value = stageMaster.TotalTime;
            TotalTime = stageMaster.TotalTime;
            CurrentPoint.Value = 0;
            MaxPoint = stageMaster.MaxPoint;

            PlayerCurrentHp = playerMaster.MaxHp;
            PlayerMaxHp = playerMaster.MaxHp;
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

        public GameStageResultData CreateStageResult()
        {
            return new GameStageResultData
            {
                StageResult = StageResult,
                CurrentTime = CurrentTime.Value,
                TotalTime = TotalTime,
                CurrentPoint = CurrentPoint.Value,
                MaxPoint = MaxPoint,
                PlayerCurrentHp = PlayerCurrentHp,
                PlayerMaxHp = PlayerMaxHp,
                NextStageId = StageMaster.NextStageId,
            };
        }
    }
}