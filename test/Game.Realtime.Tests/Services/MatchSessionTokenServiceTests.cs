using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchSessionTokenService のテスト
/// </summary>
public class MatchSessionTokenServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<MatchSessionTokenService>> _loggerMock;
    private readonly MatchSessionTokenService _service;

    public MatchSessionTokenServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<MatchSessionTokenService>>();

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new MatchSessionTokenService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task IssueTokenAsync_ReturnsNonEmptyToken()
    {
        // Arrange
        _dbMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var token = await _service.IssueTokenAsync("user123", "match456");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task IssueTokenAsync_StoresTokenInRedis()
    {
        // Arrange
        _dbMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.IssueTokenAsync("user123", "match456");

        // Assert
        _dbMock.Verify(
            x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("session:token:")),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenTokenNotFound()
    {
        // Arrange
        _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.ValidateTokenAsync("nonexistent-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTokenInfo_WhenTokenExists()
    {
        // Arrange
        var json = """{"UserId":"user123","MatchId":"match456","ExpiresAt":"2026-01-01T00:00:00+00:00"}""";
        _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        // Act
        var result = await _service.ValidateTokenAsync("some-token");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user123", result!.UserId);
        Assert.Equal("match456", result.MatchId);
    }

    [Fact]
    public async Task RevokeTokenAsync_DeletesTokenFromRedis()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RevokeTokenAsync("some-token");

        // Assert
        _dbMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("session:token:")),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task IssueTokenAsync_GeneratesUniqueTokens()
    {
        // Arrange
        _dbMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var token1 = await _service.IssueTokenAsync("user1", "match1");
        var token2 = await _service.IssueTokenAsync("user2", "match2");

        // Assert
        Assert.NotEqual(token1, token2);
    }
}
