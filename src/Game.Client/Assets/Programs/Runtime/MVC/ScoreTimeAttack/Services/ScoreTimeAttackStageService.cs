using System.Collections.Generic;
using System.Linq;
using Game.ScoreTimeAttack.Data;

namespace Game.ScoreTimeAttack.Services
{
    public class ScoreTimeAttackStageService : IScoreTimeAttackStageService
    {
        private readonly Dictionary<int, ScoreTimeAttackStageResultData> _gameStageResults = new();

        public bool TryAddResult(ScoreTimeAttackStageResultData result)
        {
            return _gameStageResults.TryAdd(result.StageId, result);
        }

        public ScoreTimeAttackStageTotalResultData CreateTotalResult()
        {
            var totalResultData = new ScoreTimeAttackStageTotalResultData
            {
                StageResults = _gameStageResults.Values.ToArray()
            };
            _gameStageResults.Clear();
            return totalResultData;
        }

        public void Shutdown()
        {
            _gameStageResults.Clear();
        }
    }
}