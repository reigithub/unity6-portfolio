using Game.Server.Shared.Exceptions;

namespace Game.Realtime.Validation;

public class MatchmakingValidator : IMatchmakingValidator
{
    public void ValidateGameMode(string gameMode)
    {
        if (string.IsNullOrWhiteSpace(gameMode) || gameMode.Length > 30)
        {
            throw new ErrorException(
                "INVALID_GAME_MODE",
                "Game mode is required and must not exceed 30 characters.");
        }
    }
}
