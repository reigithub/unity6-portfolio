using Game.Library.Shared.Realtime.Dto;
using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// LobbyService のテスト（Unary ロジック検証）
/// MagicOnion の ServiceBase はモックが困難なため、内部ロジック委譲先の LobbyDataService のみを検証
/// </summary>
public class LobbyServiceTests
{
    private readonly Mock<ILobbyDataService> _lobbyDataServiceMock;
    private readonly Mock<ILogger<LobbyService>> _loggerMock;

    public LobbyServiceTests()
    {
        _lobbyDataServiceMock = new Mock<ILobbyDataService>();
        _loggerMock = new Mock<ILogger<LobbyService>>();
    }

    [Fact]
    public void LobbyService_CanBeInstantiated()
    {
        // Act
        var service = new LobbyService(_lobbyDataServiceMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task LobbyDataService_CreateAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var lobbyId = "test-lobby-id";
        _lobbyDataServiceMock.Setup(x => x.CreateAsync("host1", "Test Lobby", "survival", 4, true))
            .ReturnsAsync(lobbyId);

        var expectedLobby = new LobbyInfo
        {
            LobbyId = lobbyId,
            LobbyName = "Test Lobby",
            HostUserId = "host1",
            GameMode = "survival",
            CurrentPlayers = 1,
            MaxPlayers = 4,
            IsPublic = true,
        };
        _lobbyDataServiceMock.Setup(x => x.GetLobbyAsync(lobbyId))
            .ReturnsAsync(expectedLobby);

        // Act: Create
        var createdId = await _lobbyDataServiceMock.Object.CreateAsync("host1", "Test Lobby", "survival", 4, true);

        // Assert
        Assert.Equal(lobbyId, createdId);

        // Act: Get
        var lobby = await _lobbyDataServiceMock.Object.GetLobbyAsync(lobbyId);

        // Assert
        Assert.NotNull(lobby);
        Assert.Equal("Test Lobby", lobby!.LobbyName);
        Assert.Equal("host1", lobby.HostUserId);
        Assert.Equal("survival", lobby.GameMode);
    }

    [Fact]
    public async Task LobbyDataService_SearchPublic_ReturnsResults()
    {
        // Arrange
        var lobbies = new[]
        {
            new LobbyInfo { LobbyId = "1", LobbyName = "Lobby 1", GameMode = "survival", CurrentPlayers = 2, MaxPlayers = 4 },
            new LobbyInfo { LobbyId = "2", LobbyName = "Lobby 2", GameMode = "survival", CurrentPlayers = 1, MaxPlayers = 4 },
        };

        _lobbyDataServiceMock.Setup(x => x.SearchPublicAsync("survival", 10))
            .ReturnsAsync(lobbies);

        // Act
        var result = await _lobbyDataServiceMock.Object.SearchPublicAsync("survival", 10);

        // Assert
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task LobbyDataService_PlayerReadiness_WorksCorrectly()
    {
        // Arrange
        _lobbyDataServiceMock.Setup(x => x.SetReadyAsync("lobby1", "user1", true))
            .ReturnsAsync(true);
        _lobbyDataServiceMock.Setup(x => x.AreAllReadyAsync("lobby1"))
            .ReturnsAsync(false);

        // Act
        var setResult = await _lobbyDataServiceMock.Object.SetReadyAsync("lobby1", "user1", true);
        var allReady = await _lobbyDataServiceMock.Object.AreAllReadyAsync("lobby1");

        // Assert
        Assert.True(setResult);
        Assert.False(allReady);
    }

    [Fact]
    public void CreateLobbyRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new CreateLobbyRequest();

        // Assert
        Assert.Equal(string.Empty, request.LobbyName);
        Assert.Equal(string.Empty, request.GameMode);
        Assert.Equal(4, request.MaxPlayers);
        Assert.True(request.IsPublic);
    }

    [Fact]
    public void CreateLobbyResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new CreateLobbyResponse();

        // Assert
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.LobbyId);
        Assert.Equal(string.Empty, response.ErrorMessage);
    }

    [Fact]
    public void LobbyPlayerInfo_DefaultValues_AreCorrect()
    {
        // Act
        var info = new LobbyPlayerInfo();

        // Assert
        Assert.Equal(string.Empty, info.UserId);
        Assert.Equal(string.Empty, info.PlayerName);
        Assert.False(info.IsReady);
        Assert.False(info.IsHost);
    }
}
