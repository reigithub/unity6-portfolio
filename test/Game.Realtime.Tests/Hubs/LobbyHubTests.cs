using Game.Realtime.Hubs;
using Game.Realtime.Services;
using Game.Realtime.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Game.Realtime.Tests.Hubs;

/// <summary>
/// LobbyHub の基本テスト
/// </summary>
public class LobbyHubTests
{
    [Fact]
    public void LobbyHub_CanBeInstantiated()
    {
        // Arrange
        var logger = new Mock<ILogger<LobbyHub>>();
        var lobbyDataService = new Mock<ILobbyDataService>();
        var tokenService = new Mock<IMatchSessionTokenService>();
        var gameServerConfig = Options.Create(new GameServerConfiguration());
        var lobbyValidator = new Mock<ILobbyValidator>();

        // Act
        var hub = new LobbyHub(
            logger.Object, lobbyDataService.Object, tokenService.Object,
            gameServerConfig, lobbyValidator.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void LobbyHub_ImplementsILobbyHub()
    {
        // Arrange
        var logger = new Mock<ILogger<LobbyHub>>();
        var lobbyDataService = new Mock<ILobbyDataService>();
        var tokenService = new Mock<IMatchSessionTokenService>();
        var gameServerConfig = Options.Create(new GameServerConfiguration());
        var lobbyValidator = new Mock<ILobbyValidator>();

        // Act
        var hub = new LobbyHub(
            logger.Object, lobbyDataService.Object, tokenService.Object,
            gameServerConfig, lobbyValidator.Object);

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.ILobbyHub>(hub);
    }
}
