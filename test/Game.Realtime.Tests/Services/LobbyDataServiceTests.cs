using Game.Realtime.Services;
using Medallion.Threading;
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
    private readonly Mock<IDistributedLockProvider> _lockProviderMock;
    private readonly LobbyDataService _service;

    public LobbyDataServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<LobbyDataService>>();
        _lockProviderMock = new Mock<IDistributedLockProvider>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        // ロックは常に成功（テスト環境ではレースコンディションなし）
        var lockMock = new Mock<IDistributedLock>();
        lockMock.Setup(x => x.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDistributedSynchronizationHandle>());
        _lockProviderMock.Setup(x => x.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        _service = new LobbyDataService(_redisMock.Object, _lockProviderMock.Object, _loggerMock.Object);
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
        var lobbyId = await _service.CreateAsync("host1", "HostPlayer", "Test Lobby", "survival", 4, true);

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
    public async Task AddPlayerAsync_ReturnsTrue_WhenSuccessful()
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
            .ReturnsAsync(RedisValue.Null);

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

        // Act
        var result = await _service.AddPlayerAsync("lobby1", "user1", "Player1");

        // Assert
        Assert.True(result);

        // プレイヤーデータが保存されたことを確認
        _dbMock.Verify(
            x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // プレイヤーの現在ロビーが記録されたことを確認
        _dbMock.Verify(
            x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:player:user1"),
                It.Is<RedisValue>(v => v.ToString() == "lobby1"),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemovePlayerAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        _dbMock.Setup(x => x.HashDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:player:user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // 残りプレイヤーがいる → ロビー削除しない
        _dbMock.Setup(x => x.HashLengthAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.RemovePlayerAsync("lobby1", "user1");

        // Assert
        Assert.True(result);
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:player:user1"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemovePlayerAsync_DeletesLobby_WhenLastPlayerLeaves()
    {
        // Arrange
        _dbMock.Setup(x => x.HashDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock.Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // 残りプレイヤー 0 → ロビー自動削除
        _dbMock.Setup(x => x.HashLengthAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(0);

        // DeleteAsync 内で必要なセットアップ
        _dbMock.Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1"),
                It.Is<RedisValue>(v => v.ToString() == "gameMode"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("survival"));

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        _dbMock.Setup(x => x.SetRemoveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.RemovePlayerAsync("lobby1", "user1");

        // Assert
        Assert.True(result);

        // ロビーデータが削除されたことを確認
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemovePlayerAsync_ReturnsFalse_WhenPlayerNotInLobby()
    {
        // Arrange
        _dbMock.Setup(x => x.HashDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.RemovePlayerAsync("lobby1", "nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPlayersAsync_ReturnsPlayerInfoWithHostFlag()
    {
        // Arrange
        var playerHash = new HashEntry[]
        {
            new("host1", """{"playerName":"HostPlayer","isReady":true,"joinedAt":1000}"""),
            new("user2", """{"playerName":"Guest","isReady":false,"joinedAt":2000}"""),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(playerHash);

        _dbMock.Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1"),
                It.Is<RedisValue>(v => v.ToString() == "hostUserId"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("host1"));

        // Act
        var players = await _service.GetPlayersAsync("lobby1");

        // Assert
        Assert.Equal(2, players.Length);

        var host = players.First(p => p.UserId == "host1");
        Assert.Equal("HostPlayer", host.PlayerName);
        Assert.True(host.IsReady);
        Assert.True(host.IsHost);

        var guest = players.First(p => p.UserId == "user2");
        Assert.Equal("Guest", guest.PlayerName);
        Assert.False(guest.IsReady);
        Assert.False(guest.IsHost);
    }

    [Fact]
    public async Task SetReadyAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        var playerJson = """{"playerName":"Player1","isReady":false,"joinedAt":1000}""";
        _dbMock.Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(playerJson));

        _dbMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SetReadyAsync("lobby1", "user1", true);

        // Assert
        Assert.True(result);

        // 更新された JSON が保存されたことを確認
        _dbMock.Verify(
            x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.Is<RedisValue>(v => v.ToString() == "user1"),
                It.Is<RedisValue>(v => v.ToString().Contains("\"isReady\":true")),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchPublicAsync_ReturnsOnlyNonFullLobbies()
    {
        // Arrange: 3つのロビーID（1つは満員）
        var lobbyIds = new RedisValue[] { "lobby1", "lobby2", "lobby3" };
        _dbMock.Setup(x => x.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:public:survival"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(lobbyIds);

        // lobby1: 空きあり
        SetupLobbyHash("lobby1", currentPlayers: 2, maxPlayers: 4);
        // lobby2: 満員
        SetupLobbyHash("lobby2", currentPlayers: 4, maxPlayers: 4);
        // lobby3: 空きあり
        SetupLobbyHash("lobby3", currentPlayers: 1, maxPlayers: 4);

        // Act
        var result = await _service.SearchPublicAsync("survival", 10);

        // Assert: 満員の lobby2 は除外される
        Assert.Equal(2, result.Length);
        Assert.Contains(result, l => l.LobbyId == "lobby1");
        Assert.Contains(result, l => l.LobbyId == "lobby3");
        Assert.DoesNotContain(result, l => l.LobbyId == "lobby2");
    }

    [Fact]
    public async Task SearchPublicAsync_RespectsMaxResults()
    {
        // Arrange: 3つのロビー
        var lobbyIds = new RedisValue[] { "lobby1", "lobby2", "lobby3" };
        _dbMock.Setup(x => x.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:public:survival"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(lobbyIds);

        SetupLobbyHash("lobby1", currentPlayers: 1, maxPlayers: 4);
        SetupLobbyHash("lobby2", currentPlayers: 1, maxPlayers: 4);
        SetupLobbyHash("lobby3", currentPlayers: 1, maxPlayers: 4);

        // Act: maxResults = 2
        var result = await _service.SearchPublicAsync("survival", 2);

        // Assert
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task AreAllReadyAsync_ReturnsTrue_WhenAllPlayersReady()
    {
        // Arrange
        var playerHash = new HashEntry[]
        {
            new("user1", """{"playerName":"P1","isReady":true,"joinedAt":1000}"""),
            new("user2", """{"playerName":"P2","isReady":true,"joinedAt":2000}"""),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(playerHash);

        // Act
        var result = await _service.AreAllReadyAsync("lobby1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AreAllReadyAsync_ReturnsFalse_WhenSomePlayersNotReady()
    {
        // Arrange
        var playerHash = new HashEntry[]
        {
            new("user1", """{"playerName":"P1","isReady":true,"joinedAt":1000}"""),
            new("user2", """{"playerName":"P2","isReady":false,"joinedAt":2000}"""),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:lobby1:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(playerHash);

        // Act
        var result = await _service.AreAllReadyAsync("lobby1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreAllReadyAsync_ReturnsFalse_WhenNoPlayers()
    {
        // Arrange
        _dbMock.Setup(x => x.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        var result = await _service.AreAllReadyAsync("lobby1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPlayerLobbyAsync_ReturnsLobbyId_WhenPlayerInLobby()
    {
        // Arrange
        _dbMock.Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lobby:player:user1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("lobby1"));

        // Act
        var result = await _service.GetPlayerLobbyAsync("user1");

        // Assert
        Assert.Equal("lobby1", result);
    }

    [Fact]
    public async Task GetPlayerLobbyAsync_ReturnsNull_WhenPlayerNotInLobby()
    {
        // Arrange
        _dbMock.Setup(x => x.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetPlayerLobbyAsync("user1");

        // Assert
        Assert.Null(result);
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

    /// <summary>
    /// SearchPublicAsync テスト用ヘルパー: 指定ロビーの Hash と players 長をセットアップ
    /// </summary>
    private void SetupLobbyHash(string lobbyId, int currentPlayers, int maxPlayers)
    {
        var hash = new HashEntry[]
        {
            new("name", $"Lobby {lobbyId}"),
            new("hostUserId", "host1"),
            new("gameMode", "survival"),
            new("maxPlayers", maxPlayers.ToString()),
            new("isPublic", "1"),
        };

        _dbMock.Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == $"lobby:{lobbyId}"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(hash);

        _dbMock.Setup(x => x.HashLengthAsync(
                It.Is<RedisKey>(k => k.ToString() == $"lobby:{lobbyId}:players"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(currentPlayers);
    }
}
