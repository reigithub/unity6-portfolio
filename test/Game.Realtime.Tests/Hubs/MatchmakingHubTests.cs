using Game.Realtime.Hubs;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Hubs;

/// <summary>
/// MatchmakingHub のテスト
/// </summary>
public class MatchmakingHubTests
{
    [Fact]
    public void MatchmakingHub_CanBeInstantiated()
    {
        // Arrange
        var logger = new Mock<ILogger<MatchmakingHub>>();
        var redis = new Mock<IConnectionMultiplexer>();

        // Act
        var hub = new MatchmakingHub(logger.Object, redis.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void MatchmakingHub_ImplementsIMatchmakingHub()
    {
        // Arrange
        var logger = new Mock<ILogger<MatchmakingHub>>();
        var redis = new Mock<IConnectionMultiplexer>();

        // Act
        var hub = new MatchmakingHub(logger.Object, redis.Object);

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.IMatchmakingHub>(hub);
    }
}
