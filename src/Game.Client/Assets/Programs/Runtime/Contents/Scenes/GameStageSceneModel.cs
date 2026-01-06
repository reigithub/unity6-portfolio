using System;
using System.Linq;
using Game.Core.MasterData;
using Game.Core.MasterData.MemoryTables;
using Game.Core.MessagePipe;
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

    public readonly struct GameStageResultData
    {
        public int StageId { get; init; }
        public GameStageResult StageResult { get; init; }

        public int CurrentTime { get; init; }
        public int TotalTime { get; init; }

        public int CurrentPoint { get; init; }
        public int MaxPoint { get; init; }

        public int PlayerCurrentHp { get; init; }
        public int PlayerMaxHp { get; init; }

        public int? NextStageId { get; init; }

        public int CalculateScore()
        {
            var remainingTime = TotalTime - Math.Abs(CurrentTime - TotalTime);
            return remainingTime * CurrentPoint * PlayerCurrentHp;
        }
    }

    public readonly struct GameStageTotalResultRequest
    {
    }

    public readonly struct GameStageTotalResultData
    {
        public GameStageResultData[] StageResults { get; init; }
    }

    public class GameStageSceneModel
    {
        private GameServiceReference<MasterDataService> _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.Reference.MemoryDatabase;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        public StageMaster StageMaster { get; private set; }
        public PlayerMaster PlayerMaster { get; private set; }

        public bool IsFirstStage { get; private set; }
        public bool IsLastStage { get; private set; }

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

            var stageMasters = MemoryDatabase.StageMasterTable.FindByGroupId(StageMaster.GroupId);
            IsFirstStage = stageMasters.Min(x => x.Order) == stageMaster.Order;
            IsLastStage = stageMasters.Max(x => x.Order) == stageMaster.Order;
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

        public GameStageResultData CreateStageResult()
        {
            var result = new GameStageResultData
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

            GlobalMessageBroker.GetRequestHandler<GameStageResultData, bool>().Invoke(result);

            return result;
        }
    }
}