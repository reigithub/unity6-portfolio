using Game.Realtime.Hubs;
using Game.Realtime.Validation;
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
        var matchmakingValidator = new Mock<IMatchmakingValidator>();

        // Act
        var hub = new MatchmakingHub(logger.Object, redis.Object, matchmakingValidator.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void MatchmakingHub_ImplementsIMatchmakingHub()
    {
        // Arrange
        var logger = new Mock<ILogger<MatchmakingHub>>();
        var redis = new Mock<IConnectionMultiplexer>();
        var matchmakingValidator = new Mock<IMatchmakingValidator>();

        // Act
        var hub = new MatchmakingHub(logger.Object, redis.Object, matchmakingValidator.Object);

        // Assert
        Assert.IsAssignableFrom<Game.Library.Shared.Realtime.Hubs.IMatchmakingHub>(hub);
    }
}
