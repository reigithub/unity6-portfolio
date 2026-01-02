using System;
using Game.Core.MasterData.MemoryTables;
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
        public StageMaster StageMaster { get; private set; }

        // Memo: データの持ち方は後日検討するとして、一旦動くものを作成
        public GameStageState StageState { get; set; }
        public GameStageResult StageResult { get; set; }

        public int CurrentTime { get; set; }
        public int TotalTime { get; set; }

        public int CurrentPoint { get; set; }
        public int MaxPoint { get; set; }

        public int PlayerCurrentHp { get; set; }
        public int PlayerMaxHp { get; set; }

        public float PlayerStamina { get; set; }
        public float PlayerMaxStamina { get; set; }

        public GameStageSceneModel()
        {
            StageState = GameStageState.None;
            StageResult = GameStageResult.None;
        }

        public void Initialize(StageMaster stageMaster)
        {
            StageMaster = stageMaster;

            CurrentTime = stageMaster.TotalTime;
            TotalTime = stageMaster.TotalTime;
            CurrentPoint = 0;
            MaxPoint = stageMaster.MaxPoint;
            PlayerCurrentHp = stageMaster.PlayerMaxHp;
            PlayerMaxHp = stageMaster.PlayerMaxHp;
            // PlayerStamina = 100f;
            // PlayerMaxStamina = 100f;
        }

        public void ProgressTime()
        {
            CurrentTime--;
            CurrentTime = Math.Max(0, CurrentTime);
        }

        public void AddPoint(int point)
        {
            CurrentPoint = Mathf.Clamp(CurrentPoint + point, 0, MaxPoint);
        }

        public void PlayerHpDamaged(int hpDamage)
        {
            PlayerCurrentHp = Mathf.Clamp(PlayerCurrentHp - hpDamage, 0, PlayerMaxHp);
        }

        public bool IsTimeUp()
        {
            return CurrentTime <= 0;
        }

        public bool IsClear()
        {
            return CurrentPoint >= MaxPoint;
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
                CurrentTime = CurrentTime,
                TotalTime = TotalTime,
                CurrentPoint = CurrentPoint,
                MaxPoint = MaxPoint,
                PlayerCurrentHp = PlayerCurrentHp,
                PlayerMaxHp = PlayerMaxHp,
                NextStageId = StageMaster.NextStageId,
            };
        }
    }
}