using Game.MVP.Core.Enums;

namespace Game.MVP.Core.Constants
{
    public static class GameSceneConstants
    {
        // プレハブを所属させる常駐UnitySceneName
        public const string GameRootScene = "GameRootScene";

        public const GameSceneOperations DefaultOperations = GameSceneOperations.CurrentSceneTerminate |
                                                             GameSceneOperations.CurrentSceneClearHistory;
    }
}