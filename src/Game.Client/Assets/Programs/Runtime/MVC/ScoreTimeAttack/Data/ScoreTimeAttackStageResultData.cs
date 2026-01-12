using System;
using System.Linq;
using Game.ScoreTimeAttack.Enums;

namespace Game.ScoreTimeAttack.Data
{
    public readonly struct ScoreTimeAttackStageResultData
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

        public int GetRemainingTime()
        {
            return TotalTime - Math.Abs(CurrentTime - TotalTime);
        }

        public int CalculateScore()
        {
            var remainingTime = GetRemainingTime();
            return remainingTime * CurrentPoint * PlayerCurrentHp;
        }
    }

    public readonly struct ScoreTimeAttackStageTotalResultData
    {
        public ScoreTimeAttackStageResultData[] StageResults { get; init; }

        public int CalculateTotalScore()
        {
            var remainingTime = StageResults.Sum(x => x.GetRemainingTime());
            var totalPoint = StageResults.Sum(x => x.CurrentPoint);
            var totalHp = StageResults.Sum(x => x.PlayerCurrentHp);
            return remainingTime * totalPoint * totalHp;
        }
    }
}