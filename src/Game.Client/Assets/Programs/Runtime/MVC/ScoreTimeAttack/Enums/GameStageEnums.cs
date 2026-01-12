namespace Game.ScoreTimeAttack.Enums
{
    public enum GameStageState
    {
        None,
        Ready,
        Start,
        Retry,
        Result,
        Finish
    }

    public enum GameStageResult
    {
        None,
        Clear,
        Failed,
    }
}