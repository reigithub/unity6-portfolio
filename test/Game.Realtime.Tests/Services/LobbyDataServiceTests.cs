using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// LobbyDataService のテスト
/// </summary>
public class LobbyDataServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<LobbyDataService>> _loggerMock;
    private readonly LobbyDataService _service;

    public LobbyDataServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<LobbyDataService>>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new LobbyDataService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_StoresLobbyInRedis()
    {
        // Arrange
        _dbMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        _dbMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.SetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var lobbyId = await _service.CreateAsync("host1", "Test Lobby", "survival", 4, true);

        // Assert
        Assert.NotNull(lobbyId);
        Assert.NotEmpty(lobbyId);

        // ロビーメタデータが保存されたことを確認
        _dbMock.Verify(
            x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("lobby:")),
                It.IsAny<HashEntry[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // 公開ロビーに追加されたことを確認
        _dbMock.Verify(
            x => x.SetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:public:survival"),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task AddPlayerAsync_ReturnsFalse_WhenLobbyNotExists()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AddPlayerAsync("nonexistent", "user1", "Player1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddPlayerAsync_ReturnsFalse_WhenLobbyFull()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "maxPlayers"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("4"));

        _dbMock.Setup(x => x.HashLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(4);

        // Act
        var result = await _service.AddPlayerAsync("lobby1", "user1", "Player1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddPlayerAsync_ReturnsFalse_WhenAlreadyInLobby()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "maxPlayers"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("4"));

        _dbMock.Setup(x => x.HashLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);

        _dbMock.Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:player:user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("existing-lobby"));

        // Act
        var result = await _service.AddPlayerAsync("lobby1", "user1", "Player1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetLobbyAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        var result = await _service.GetLobbyAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLobbyAsync_ReturnsLobbyInfo_WhenExists()
    {
        // Arrange
        var hash = new HashEntry[]
        {
            new("name", "Test Lobby"),
            new("hostUserId", "host1"),
            new("gameMode", "survival"),
            new("maxPlayers", "4"),
            new("isPublic", "1"),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:testlobby"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(hash);

        _dbMock.Setup(x => x.HashLengthAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:testlobby:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);

        // Act
        var result = await _service.GetLobbyAsync("testlobby");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testlobby", result!.LobbyId);
        Assert.Equal("Test Lobby", result.LobbyName);
        Assert.Equal("host1", result.HostUserId);
        Assert.Equal("survival", result.GameMode);
        Assert.Equal(2, result.CurrentPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.True(result.IsPublic);
    }

    [Fact]
    public async Task SetReadyAsync_ReturnsFalse_WhenPlayerNotInLobby()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.SetReadyAsync("lobby1", "user1", true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAllLobbyData()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue>(v => v.ToString() == "gameMode"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("survival"));

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new HashEntry[]
            {
                new("user1", "{}"),
                new("user2", "{}"),
            });

        _dbMock.Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.SetRemoveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.DeleteAsync("lobby1");

        // Assert: プレイヤーの参加記録が削除されたことを確認
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("lobby:player:")),
                It.IsAny<CommandFlags>()),
            Times.Exactly(2));

        // Assert: ロビーデータが削除されたことを確認
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1"),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // Assert: 公開ロビー一覧から削除されたことを確認
        _dbMock.Verify(
            x => x.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:public:survival"),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
