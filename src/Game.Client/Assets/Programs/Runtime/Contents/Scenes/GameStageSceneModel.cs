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

        public int Point { get; set; }
        public int MaxPoint { get; set; }

        public int PlayerHp { get; set; }
        public int PlayerMaxHp { get; set; }
    }

    public class GameStageSceneModel
    {
        // Memo: データの持ち方は後日検討するとして、一旦動くものを作成
        public GameStageState StageState { get; set; }
        public GameStageResult StageResult { get; set; }
        public int Point { get; set; }
        public int MaxPoint { get; set; }

        public int PlayerHp { get; set; }
        public int PlayerMaxHp { get; set; }
        public float PlayerStamina { get; set; }
        public float PlayerMaxStamina { get; set; }

        // public ReactiveProperty<int> Mp { get; set; }

        public GameStageSceneModel()
        {
            StageState = GameStageState.None;
            StageResult = GameStageResult.None;
            Point = 0;
            MaxPoint = 5;

            PlayerHp = 1;
            PlayerMaxHp = 1;
            PlayerStamina = 100f;
            PlayerMaxStamina = 100f;
        }

        public void AddPoint(int point)
        {
            Point = Mathf.Clamp(Point + point, 0, MaxPoint);
        }

        public void PlayerHpDamaged(int hp)
        {
            PlayerHp = Mathf.Clamp(PlayerHp - hp, 0, PlayerMaxHp);
        }

        public bool IsClear()
        {
            return Point >= MaxPoint;
        }

        public bool IsFailed()
        {
            return PlayerHp <= 0;
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
                Point = Point,
                MaxPoint = MaxPoint,
                PlayerHp = PlayerHp,
                PlayerMaxHp = PlayerMaxHp
            };
        }
    }
}