using Game.Realtime.Validation;
using Game.Server.Shared.Exceptions;

namespace Game.Realtime.Tests.Validation;

public class MatchmakingValidatorTests
{
    private readonly MatchmakingValidator _validator = new();

    [Theory]
    [InlineData("survival")]
    [InlineData("deathmatch")]
    [InlineData("a")]
    public void ValidateGameMode_ValidInput_DoesNotThrow(string gameMode)
    {
        _validator.ValidateGameMode(gameMode);
    }

    [Fact]
    public void ValidateGameMode_ExactlyMaxLength_DoesNotThrow()
    {
        var gameMode = new string('a', 30);
        _validator.ValidateGameMode(gameMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateGameMode_NullOrWhitespace_ThrowsErrorException(string? gameMode)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateGameMode(gameMode!));
        Assert.Equal("INVALID_GAME_MODE", ex.ErrorCode);
    }

    [Fact]
    public void ValidateGameMode_ExceedsMaxLength_ThrowsErrorException()
    {
        var gameMode = new string('a', 31);
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateGameMode(gameMode));
        Assert.Equal("INVALID_GAME_MODE", ex.ErrorCode);
    }
}
