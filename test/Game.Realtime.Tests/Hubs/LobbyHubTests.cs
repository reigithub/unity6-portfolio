using Game.Realtime.Hubs;
using Microsoft.Extensions.Logging;
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

        // Act
        var hub = new LobbyHub(logger.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void LobbyHub_ImplementsILobbyHub()
    {
        // Arrange
        var logger = new Mock<ILogger<LobbyHub>>();

        // Act
        var hub = new LobbyHub(logger.Object);

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.ILobbyHub>(hub);
    }
}
