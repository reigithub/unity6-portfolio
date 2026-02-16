using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchmakingQueueService のテスト
/// </summary>
public class MatchmakingQueueServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<MatchmakingQueueService>> _loggerMock;
    private readonly MatchmakingQueueService _service;

    public MatchmakingQueueServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<MatchmakingQueueService>>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new MatchmakingQueueService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task EnqueuePlayerAsync_AddsSortedSetEntry()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.EnqueuePlayerAsync("user1", "survival");

        // Assert
        _dbMock.Verify(
            x => x.SortedSetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "matchmaking:queue:survival"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task DequeuePlayerAsync_RemovesSortedSetEntry()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetRemoveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.DequeuePlayerAsync("user1", "survival");

        // Assert
        _dbMock.Verify(
            x => x.SortedSetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "matchmaking:queue:survival"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetQueueCountAsync_ReturnsSortedSetLength()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        // Act
        var count = await _service.GetQueueCountAsync("survival");

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task DequeueTopPlayersAsync_ReturnsPlayerIds()
    {
        // Arrange
        var entries = new SortedSetEntry[]
        {
            new("player1", 1000),
            new("player2", 2000),
            new("player3", 3000),
            new("player4", 4000),
        };

        _dbMock.Setup(x => x.SortedSetPopAsync(
                It.Is<RedisKey>(k => k.ToString() == "matchmaking:queue:survival"),
                It.Is<long>(c => c == 4),
                It.Is<Order>(o => o == Order.Ascending),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _service.DequeueTopPlayersAsync("survival", 4);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal("player1", result[0]);
        Assert.Equal("player2", result[1]);
        Assert.Equal("player3", result[2]);
        Assert.Equal("player4", result[3]);
    }

    [Fact]
    public async Task DequeueTopPlayersAsync_ReturnsEmptyWhenQueueEmpty()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetPopAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<SortedSetEntry>());

        // Act
        var result = await _service.DequeueTopPlayersAsync("survival", 4);

        // Assert
        Assert.Empty(result);
    }
}
