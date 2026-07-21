using Game.Core.Services;
using Game.ScoreTimeAttack.Data;
using Game.Shared.Services.Interfaces;

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
