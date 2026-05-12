using Game.MVP.Core.Enums;

namespace Game.MVP.Core.Constants
{
    public static class GameSceneConstants
    {
        public const string GameRootSceneName = "GameRootScene";

        public const GameSceneOperations DefaultOperations = GameSceneOperations.CurrentSceneTerminate |
                                                             GameSceneOperations.CurrentSceneClearHistory;
    }
}
