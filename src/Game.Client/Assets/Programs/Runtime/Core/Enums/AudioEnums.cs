using System;

namespace Game.Core.Enums
{
    public enum AudioCategory
    {
        None = 0,
        Bgm = 1,
        Voice = 2,
        SoundEffect = 3,
    }

    public enum AudioPlayTag
    {
        None = 0,
        GameReady = 1,
        GameStart = 2,
        GameQuit = 3,

        Loading = 1000,

        StageReady = 5000,
        StageStart = 5001,
        StagePause = 5010,
        StageResume = 5011,
        StageRetry = 5020,
        StageReturnTitle = 5021,
        StageClear = 5030,
        StageFailed = 5031,
        StageFinish = 5040,

        PlayerRun = 7000,
        PlayerJump = 7001,
        PlayerDamaged = 7020,
        PlayerDown = 7021,
        PlayerGetUp = 7022,
        PlayerStaminaFull = 7030,
        PlayerStaminaDepleted = 7031,
        PlayerGetPoint = 7040,

        UIButton = 9000,
        UIOpen = 9001,
        UIClose = 9002,
    }
}