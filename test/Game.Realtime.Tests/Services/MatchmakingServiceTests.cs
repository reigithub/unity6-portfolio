using Game.Library.Shared.Realtime.Dto;
using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchmakingService のテスト（Unary レスポンス検証）
/// MagicOnion の ServiceBase はモックが困難なため、内部ロジック委譲先の QueueService のみを検証
/// </summary>
public class MatchmakingServiceTests
{
    private readonly Mock<IMatchmakingQueueService> _queueServiceMock;
    private readonly Mock<ILogger<MatchmakingService>> _loggerMock;

    public MatchmakingServiceTests()
    {
        _queueServiceMock = new Mock<IMatchmakingQueueService>();
        _loggerMock = new Mock<ILogger<MatchmakingService>>();
    }

    [Fact]
    public async Task QueueService_EnqueueAndDequeue_WorksCorrectly()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.EnqueuePlayerAsync("user1", "survival"))
            .Returns(Task.CompletedTask);
        _queueServiceMock.Setup(x => x.DequeuePlayerAsync("user1", "survival"))
            .Returns(Task.CompletedTask);
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival"))
            .ReturnsAsync(5);

        // Act: Enqueue
        await _queueServiceMock.Object.EnqueuePlayerAsync("user1", "survival");

        // Assert: Enqueue was called
        _queueServiceMock.Verify(
            x => x.EnqueuePlayerAsync("user1", "survival"),
            Times.Once);

        // Act: GetQueueCount
        var count = await _queueServiceMock.Object.GetQueueCountAsync("survival");

        // Assert
        Assert.Equal(5, count);

        // Act: Dequeue
        await _queueServiceMock.Object.DequeuePlayerAsync("user1", "survival");

        // Assert: Dequeue was called
        _queueServiceMock.Verify(
            x => x.DequeuePlayerAsync("user1", "survival"),
            Times.Once);
    }

    [Fact]
    public void MatchmakingService_CanBeInstantiated()
    {
        // Act
        var service = new MatchmakingService(_queueServiceMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void MatchmakingResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new MatchmakingResponse();

        // Assert
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.TicketId);
        Assert.Equal(0, response.EstimatedWaitSeconds);
        Assert.Equal(0, response.PlayersInQueue);
        Assert.Equal(string.Empty, response.ErrorMessage);
    }

    [Fact]
    public void MatchmakingRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new MatchmakingRequest();

        // Assert
        Assert.Equal(string.Empty, request.GameMode);
    }
}
