using System;

namespace Game.Core.Enums
{
    public enum GameSceneState
    {
        None = 0,
        Processing,
        Sleep,
        Terminate
    }

    [Flags]
    public enum GameSceneOperations
    {
        None = 0,
        CurrentSceneSleep = 1 << 0,
        CurrentSceneRestart = 1 << 1,
        CurrentSceneTerminate = 1 << 2,
        CurrentSceneClearHistory = 1 << 3,
    }
}