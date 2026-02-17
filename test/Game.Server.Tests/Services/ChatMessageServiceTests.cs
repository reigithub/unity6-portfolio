using Game.Library.Shared.Chat.Dto;
using Game.Server.Services.Chat;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Server.Tests.Services;

/// <summary>
/// ChatMessageService のテスト
/// </summary>
public class ChatMessageServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<ChatMessageService>> _loggerMock;
    private readonly ChatMessageService _service;

    public ChatMessageServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<ChatMessageService>>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new ChatMessageService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SaveMessageAsync_StoresMessageInSortedSet()
    {
        // Arrange
        var message = new ChatMessage
        {
            UserId = "user1",
            PlayerName = "Player1",
            Content = "Hello!",
            Timestamp = 1000000,
        };

        _dbMock.Setup(x => x.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        // Act
        await _service.SaveMessageAsync("room1", message);

        // Assert
        _dbMock.Verify(
            x => x.SortedSetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "chat:messages:room1"),
                It.IsAny<RedisValue>(),
                It.Is<double>(s => s == 1000000),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveMessageAsync_TrimsOldMessages_WhenExceedsMax()
    {
        // Arrange
        var message = new ChatMessage
        {
            UserId = "user1",
            PlayerName = "Player1",
            Content = "Hello!",
            Timestamp = 1000000,
        };

        _dbMock.Setup(x => x.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(250);

        _dbMock.Setup(x => x.SortedSetRemoveRangeByRankAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(50);

        // Act
        await _service.SaveMessageAsync("room1", message);

        // Assert
        _dbMock.Verify(
            x => x.SortedSetRemoveRangeByRankAsync(
                It.Is<RedisKey>(k => k.ToString() == "chat:messages:room1"),
                It.Is<long>(start => start == 0),
                It.Is<long>(stop => stop == 49),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsEmptyArray_WhenNoMessages()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetRangeByRankAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        // Act
        var result = await _service.GetRecentMessagesAsync("room1", 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsMessages_InChronologicalOrder()
    {
        // Arrange
        var json1 = """{"userId":"user1","playerName":"Player1","content":"First","timestamp":1000}""";
        var json2 = """{"userId":"user2","playerName":"Player2","content":"Second","timestamp":2000}""";

        _dbMock.Setup(x => x.SortedSetRangeByRankAsync(
                It.Is<RedisKey>(k => k.ToString() == "chat:messages:room1"),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.Is<Order>(o => o == Order.Ascending),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { json1, json2 });

        // Act
        var result = await _service.GetRecentMessagesAsync("room1", 10);

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal("user1", result[0].UserId);
        Assert.Equal("First", result[0].Content);
        Assert.Equal(1000, result[0].Timestamp);
        Assert.Equal("user2", result[1].UserId);
        Assert.Equal("Second", result[1].Content);
        Assert.Equal(2000, result[1].Timestamp);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_RequestsCorrectRange()
    {
        // Arrange
        _dbMock.Setup(x => x.SortedSetRangeByRankAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        // Act
        await _service.GetRecentMessagesAsync("room1", 50);

        // Assert
        _dbMock.Verify(
            x => x.SortedSetRangeByRankAsync(
                It.Is<RedisKey>(k => k.ToString() == "chat:messages:room1"),
                It.Is<long>(start => start == -50),
                It.Is<long>(stop => stop == -1),
                It.Is<Order>(o => o == Order.Ascending),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
