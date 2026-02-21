using Game.MVP.Core.Enums;

namespace Game.MVP.Core.Constants
{
    public static class GameSceneConstants
    {
        public const GameSceneOperations DefaultOperations = GameSceneOperations.CurrentSceneTerminate |
                                                             GameSceneOperations.CurrentSceneClearHistory;
    }
}