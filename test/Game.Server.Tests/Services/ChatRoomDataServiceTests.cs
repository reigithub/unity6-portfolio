using Game.Server.Services.Chat;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Server.Tests.Services;

/// <summary>
/// ChatRoomDataService のテスト
/// </summary>
public class ChatRoomDataServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<ChatRoomDataService>> _loggerMock;
    private readonly ChatRoomDataService _service;

    public ChatRoomDataServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<ChatRoomDataService>>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new ChatRoomDataService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_StoresRoomInRedis_ReturnsRoomId()
    {
        // Arrange
        _dbMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        var roomId = await _service.CreateAsync("Test Room", "general", 10, 7);

        // Assert
        Assert.NotNull(roomId);
        Assert.NotEmpty(roomId);

        _dbMock.Verify(
            x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("chatroom:")),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenRoomExists()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ExistsAsync("room1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenRoomNotExists()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ExistsAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenRoomNotExists()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AddMemberAsync("nonexistent", "user1", "Player1", 7);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenRoomFull()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "maxMembers"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("4"));

        _dbMock.Setup(x => x.HashLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(4);

        // Act
        var result = await _service.AddMemberAsync("room1", "user1", "Player1", 7);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsTrue_WhenMaxMembersZero()
    {
        // Arrange (maxMembers=0 は無制限)
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "maxMembers"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("0"));

        _dbMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AddMemberAsync("room1", "user1", "Player1", 7);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsTrue_WhenMemberRemoved()
    {
        // Arrange
        _dbMock.Setup(x => x.HashDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1:members"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.RemoveMemberAsync("room1", "user1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_DoesNotDeleteRoom_WhenEmpty()
    {
        // Arrange
        _dbMock.Setup(x => x.HashDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RemoveMemberAsync("room1", "user1");

        // Assert: KeyDeleteAsync は呼ばれない
        _dbMock.Verify(
            x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        var result = await _service.GetRoomAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsChatRoomInfo_WhenExists()
    {
        // Arrange
        var hash = new HashEntry[]
        {
            new("name", "Test Room"),
            new("roomType", "general"),
            new("maxMembers", "10"),
            new("createdAt", "1000000"),
            new("defaultPermissions", "7"),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(hash);

        _dbMock.Setup(x => x.HashLengthAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1:members"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(3);

        // Act
        var result = await _service.GetRoomAsync("room1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("room1", result!.RoomId);
        Assert.Equal("Test Room", result.RoomName);
        Assert.Equal("general", result.RoomType);
        Assert.Equal(3, result.CurrentMembers);
        Assert.Equal(10, result.MaxMembers);
        Assert.Equal(1000000, result.CreatedAt);
        Assert.Equal(7, result.DefaultPermissions);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_ReturnsZero_WhenNotMember()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetMemberPermissionsAsync("room1", "nonmember");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_ReturnsPermissions_WhenMember()
    {
        // Arrange
        var memberJson = """{"playerName":"Player1","joinedAt":1000,"permissions":255}""";
        _dbMock.Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1:members"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(memberJson));

        // Act
        var result = await _service.GetMemberPermissionsAsync("room1", "user1");

        // Assert
        Assert.Equal(255, result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRoomAndMembers()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.DeleteAsync("room1");

        // Assert
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1:members"),
                It.IsAny<CommandFlags>()),
            Times.Once);

        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SetMemberPermissionsAsync_ReturnsFalse_WhenNotMember()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.SetMemberPermissionsAsync("room1", "nonmember", 255);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetDefaultPermissionsAsync_ReturnsDefaultPermissions()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "chatroom:room1"),
                It.Is<RedisValue>(v => v.ToString() == "defaultPermissions"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("7"));

        // Act
        var result = await _service.GetDefaultPermissionsAsync("room1");

        // Assert
        Assert.Equal(7, result);
    }
}
