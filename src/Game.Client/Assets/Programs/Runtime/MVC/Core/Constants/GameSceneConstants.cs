using Game.MVC.Core.Enums;

namespace Game.MVC.Core.Constants
{
    public static class GameSceneConstants
    {
        public const GameSceneOperations DefaultOperations = GameSceneOperations.CurrentSceneTerminate |
                                                             GameSceneOperations.CurrentSceneClearHistory;
    }
}
