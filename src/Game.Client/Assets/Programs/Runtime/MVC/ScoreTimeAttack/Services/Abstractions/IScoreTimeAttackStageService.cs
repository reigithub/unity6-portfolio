using Game.Core.Services;
using Game.ScoreTimeAttack.Data;

namespace Game.ScoreTimeAttack.Services
{
    /// <summary>
    /// ゲームステージ管理サービスのインターフェース
    /// </summary>
    public interface IScoreTimeAttackStageService : IGameService
    {
        bool TryAddResult(ScoreTimeAttackStageResultData result);
        ScoreTimeAttackStageTotalResultData CreateTotalResult();
    }
}