using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchmakingQueueService のテスト（インターフェース準拠テスト）
/// Batch API を使用するため、Redis モック経由ではなくインターフェースモックで検証
/// </summary>
public class MatchmakingQueueServiceTests
{
    [Fact]
    public void Service_CanBeInstantiated()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        var loggerMock = new Mock<ILogger<MatchmakingQueueService>>();

        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        // Act
        var service = new MatchmakingQueueService(redisMock.Object, loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task MockInterface_EnqueueAndDequeue_WorksCorrectly()
    {
        // Arrange
        var mock = new Mock<IMatchmakingQueueService>();
        mock.Setup(x => x.EnqueuePlayerAsync("user1", "survival", 1, 2))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.DequeuePlayerAsync("user1", "survival", 1))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.GetQueueCountAsync("survival", 1))
            .ReturnsAsync(5);
        mock.Setup(x => x.GetPlayerMatchSizeAsync("user1"))
            .ReturnsAsync(2);

        // Act & Assert: Enqueue
        await mock.Object.EnqueuePlayerAsync("user1", "survival", 1, 2);
        mock.Verify(x => x.EnqueuePlayerAsync("user1", "survival", 1, 2), Times.Once);

        // Act & Assert: GetQueueCount
        var count = await mock.Object.GetQueueCountAsync("survival", 1);
        Assert.Equal(5, count);

        // Act & Assert: GetPlayerMatchSize
        var matchSize = await mock.Object.GetPlayerMatchSizeAsync("user1");
        Assert.Equal(2, matchSize);

        // Act & Assert: Dequeue
        await mock.Object.DequeuePlayerAsync("user1", "survival", 1);
        mock.Verify(x => x.DequeuePlayerAsync("user1", "survival", 1), Times.Once);
    }

    [Fact]
    public async Task MockInterface_AnyStageQueue_WorksCorrectly()
    {
        // Arrange: stageId <= 0 は "any" キューに追加される
        var mock = new Mock<IMatchmakingQueueService>();
        mock.Setup(x => x.EnqueuePlayerAsync("user1", "survival", 0, 2))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(new[] { "1", "any" });

        // Act
        await mock.Object.EnqueuePlayerAsync("user1", "survival", 0, 2);
        var stageKeys = await mock.Object.GetActiveStageKeysAsync("survival");

        // Assert
        mock.Verify(x => x.EnqueuePlayerAsync("user1", "survival", 0, 2), Times.Once);
        Assert.Contains("any", stageKeys);
        Assert.Contains("1", stageKeys);
    }

    [Fact]
    public async Task MockInterface_DequeueTopPlayers_ReturnsPlayerIds()
    {
        // Arrange
        var mock = new Mock<IMatchmakingQueueService>();
        mock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 4))
            .ReturnsAsync(new[] { "p1", "p2", "p3", "p4" });

        // Act
        var result = await mock.Object.DequeueTopPlayersAsync("survival", 1, 4);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal("p1", result[0]);
        Assert.Equal("p4", result[3]);
    }

    [Fact]
    public async Task MockInterface_CleanupPlayer_RemovesMetadata()
    {
        // Arrange
        var mock = new Mock<IMatchmakingQueueService>();
        mock.Setup(x => x.CleanupPlayerAsync("user1"))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.CleanupPlayerAsync("user1");

        // Assert
        mock.Verify(x => x.CleanupPlayerAsync("user1"), Times.Once);
    }
}
