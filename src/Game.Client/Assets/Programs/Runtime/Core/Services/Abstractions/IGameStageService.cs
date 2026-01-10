using Game.Contents.Scenes;

namespace Game.Core.Services
{
    /// <summary>
    /// ゲームステージ管理サービスのインターフェース
    /// </summary>
    public interface IGameStageService : IGameService
    {
        bool TryAddResult(GameStageResultData result);
        GameStageTotalResultData CreateTotalResult();
    }
}
