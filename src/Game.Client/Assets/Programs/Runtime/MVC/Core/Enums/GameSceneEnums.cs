using System;

namespace Game.MVC.Core.Enums
{
    public enum GameSceneState
    {
        None = 0,
        Processing,
        Sleep,
        Terminate
    }

    public enum GameSceneFocusState
    {
        Focused,
        Unfocused,
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
