using System;

namespace Game.MVP.Core.Enums
{
    /// <summary>
    /// シーン状態
    /// </summary>
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
        Sleep = 1 << 0,
        Restart = 1 << 1,
        Terminate = 1 << 2,
        ClearHistory = 1 << 3,
    }
}
