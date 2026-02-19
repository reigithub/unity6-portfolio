using System.Data;
using Game.Server.Database;
using Game.Server.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace Game.Server.Tests.Health;

public class PostgresHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Connected_ReturnsHealthy()
    {
        var mockConnection = new Mock<IDbConnection>();
        var mockFactory = new Mock<IDbConnectionFactory>();
        mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);

        var check = new PostgresHealthCheck(mockFactory.Object);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Disconnected_ReturnsUnhealthy()
    {
        var mockFactory = new Mock<IDbConnectionFactory>();
        mockFactory.Setup(f => f.CreateConnection())
            .Throws(new InvalidOperationException("Connection failed"));

        var check = new PostgresHealthCheck(mockFactory.Object);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
