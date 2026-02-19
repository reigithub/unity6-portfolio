using Game.Server.Shared.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace Game.Server.Tests.Health;

public class ValkeyHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Connected_ReturnsHealthy()
    {
        var mockDb = new Mock<IDatabase>();
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        var check = new ValkeyHealthCheck(mockMultiplexer.Object);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Disconnected_ReturnsUnhealthy()
    {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"));

        var check = new ValkeyHealthCheck(mockMultiplexer.Object);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
