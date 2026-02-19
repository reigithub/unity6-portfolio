using Game.Library.Shared.Dto;
using Game.Realtime.Validation;
using Game.Server.Shared.Exceptions;

namespace Game.Realtime.Tests.Validation;

public class LobbyValidatorTests
{
    private readonly LobbyValidator _validator = new();

    // --- ValidateLobbyId ---

    [Theory]
    [InlineData("lobby-123")]
    [InlineData("a")]
    public void ValidateLobbyId_ValidInput_DoesNotThrow(string lobbyId)
    {
        _validator.ValidateLobbyId(lobbyId);
    }

    [Fact]
    public void ValidateLobbyId_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidateLobbyId(new string('a', 64));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateLobbyId_NullOrWhitespace_ThrowsErrorException(string? lobbyId)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateLobbyId(lobbyId!));
        Assert.Equal("INVALID_LOBBY_ID", ex.ErrorCode);
    }

    [Fact]
    public void ValidateLobbyId_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateLobbyId(new string('a', 65)));
        Assert.Equal("INVALID_LOBBY_ID", ex.ErrorCode);
    }

    // --- ValidatePlayerName ---

    [Theory]
    [InlineData("Player1")]
    [InlineData("a")]
    public void ValidatePlayerName_ValidInput_DoesNotThrow(string name)
    {
        _validator.ValidatePlayerName(name);
    }

    [Fact]
    public void ValidatePlayerName_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidatePlayerName(new string('a', 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePlayerName_NullOrWhitespace_ThrowsErrorException(string? name)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidatePlayerName(name!));
        Assert.Equal("INVALID_PLAYER_NAME", ex.ErrorCode);
    }

    [Fact]
    public void ValidatePlayerName_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidatePlayerName(new string('a', 51)));
        Assert.Equal("INVALID_PLAYER_NAME", ex.ErrorCode);
    }

    // --- ValidateCreateLobbyRequest ---

    [Fact]
    public void ValidateCreateLobbyRequest_ValidRequest_DoesNotThrow()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "My Lobby",
            GameMode = "survival",
            PlayerName = "Host",
            MaxPlayers = 4,
        };
        _validator.ValidateCreateLobbyRequest(request);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_MinMaxPlayers_DoesNotThrow()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby", GameMode = "mode", PlayerName = "P", MaxPlayers = 2,
        };
        _validator.ValidateCreateLobbyRequest(request);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_MaxMaxPlayers_DoesNotThrow()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby", GameMode = "mode", PlayerName = "P", MaxPlayers = 16,
        };
        _validator.ValidateCreateLobbyRequest(request);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_EmptyLobbyName_Throws()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "", GameMode = "mode", PlayerName = "P", MaxPlayers = 4,
        };
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateCreateLobbyRequest(request));
        Assert.Equal("INVALID_LOBBY_REQUEST", ex.ErrorCode);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_LobbyNameTooLong_Throws()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = new string('a', 51), GameMode = "mode", PlayerName = "P", MaxPlayers = 4,
        };
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateCreateLobbyRequest(request));
        Assert.Equal("INVALID_LOBBY_REQUEST", ex.ErrorCode);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_MaxPlayersTooLow_Throws()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby", GameMode = "mode", PlayerName = "P", MaxPlayers = 1,
        };
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateCreateLobbyRequest(request));
        Assert.Equal("INVALID_LOBBY_REQUEST", ex.ErrorCode);
    }

    [Fact]
    public void ValidateCreateLobbyRequest_MaxPlayersTooHigh_Throws()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby", GameMode = "mode", PlayerName = "P", MaxPlayers = 17,
        };
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateCreateLobbyRequest(request));
        Assert.Equal("INVALID_LOBBY_REQUEST", ex.ErrorCode);
    }

    // --- ValidateGameMode ---

    [Fact]
    public void ValidateGameMode_ValidInput_DoesNotThrow()
    {
        _validator.ValidateGameMode("survival");
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
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateGameMode(new string('a', 31)));
        Assert.Equal("INVALID_GAME_MODE", ex.ErrorCode);
    }

    // --- ValidateLobbyMessage ---

    [Fact]
    public void ValidateLobbyMessage_ValidInput_DoesNotThrow()
    {
        _validator.ValidateLobbyMessage("Hello!");
    }

    [Fact]
    public void ValidateLobbyMessage_ExactlyMaxLength_DoesNotThrow()
    {
        _validator.ValidateLobbyMessage(new string('a', 200));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateLobbyMessage_NullOrWhitespace_ThrowsErrorException(string? message)
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateLobbyMessage(message!));
        Assert.Equal("INVALID_MESSAGE", ex.ErrorCode);
    }

    [Fact]
    public void ValidateLobbyMessage_ExceedsMaxLength_ThrowsErrorException()
    {
        var ex = Assert.Throws<ErrorException>(() => _validator.ValidateLobbyMessage(new string('a', 201)));
        Assert.Equal("INVALID_MESSAGE", ex.ErrorCode);
    }
}
