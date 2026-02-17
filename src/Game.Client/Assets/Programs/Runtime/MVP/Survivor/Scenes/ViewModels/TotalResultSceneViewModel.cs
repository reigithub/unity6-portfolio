using System.Linq;
using Game.MVP.Survivor.SaveData;
using Game.Library.Shared.Dto;

namespace Game.MVP.Survivor.Scenes.ViewModels
{
    /// <summary>
    /// 総合リザルトシーンのUIロジック（テスト可能な純粋C#クラス）
    /// 勝敗判定・スコア送信DTO構築を担当
    /// </summary>
    public class TotalResultSceneViewModel
    {
        public bool IsOverallVictory(SurvivorStageSession session)
        {
            if (session.StageResults.Count == 0) return false;
            return session.StageResults.All(r => r.IsVictory);
        }

        public ScoreSubmitDto BuildScoreRequest(
            SurvivorStageResultData result, int currentWave)
        {
            return new ScoreSubmitDto
            {
                StageId = result.StageId,
                Score = result.Score,
                ClearTime = result.ClearTime,
                WaveReached = currentWave,
                EnemiesDefeated = result.Kills
            };
        }
    }
}
