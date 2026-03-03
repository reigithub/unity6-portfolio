using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchSessionTokenService のテスト（HMAC + Valkey ハイブリッド版）
/// </summary>
public class MatchSessionTokenServiceTests
{
    private const string TestSecretKey = "test-secret-key-for-hmac-signing";

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

        // SE.Redis 2.11: StringSetAsync は Expiration 型の非インターフェースオーバーロードに
        // ルーティングされるため、SetReturnsDefault で全オーバーロードをカバー
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        var settings = Options.Create(new UnityServerAuthSettings { SecretKey = TestSecretKey });
        _service = new MatchSessionTokenService(_redisMock.Object, settings, _loggerMock.Object);
    }

    [Fact]
    public async Task IssueTokenAsync_ReturnsNonEmptyToken()
    {
        // Act
        var token = await _service.IssueTokenAsync("user123", "match456");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Contains(".", token); // HMAC 形式: payload.signature
    }

    [Fact]
    public async Task IssueTokenAsync_StoresTokenInValkey()
    {
        // Act
        await _service.IssueTokenAsync("user123", "match456");

        // Assert — session:token: プレフィックスで Valkey に保存されたことを検証
        var setInvocations = _dbMock.Invocations
            .Where(i => i.Method.Name == "StringSetAsync")
            .ToList();
        Assert.Single(setInvocations);
        Assert.StartsWith("session:token:", setInvocations[0].Arguments[0].ToString()!);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenHmacInvalid()
    {
        // Act — 不正なトークン（HMAC 検証失敗）
        var result = await _service.ValidateTokenAsync("invalid-token-without-signature");

        // Assert
        Assert.Null(result);

        // Valkey への問い合わせが行われないことを検証（HMAC で早期リジェクト）
        _dbMock.Verify(
            x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenRevokedInValkey()
    {
        // Arrange — 正しいトークンを発行
        var token = await _service.IssueTokenAsync("user123", "match456");

        // Valkey から削除済み（revoke 済み）
        _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.ValidateTokenAsync(token);

        // Assert
        Assert.Null(result);

        // Valkey への問い合わせが行われたことを検証（HMAC 通過後の失効チェック）
        _dbMock.Verify(
            x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("session:token:")),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTokenInfo_WhenValid()
    {
        // Arrange — トークン発行
        var token = await _service.IssueTokenAsync("user123", "match456");

        // Valkey にトークンが存在（revoke されていない）
        var json = """{"UserId":"user123","MatchId":"match456","ExpiresAt":"2099-01-01T00:00:00+00:00"}""";
        _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        // Act
        var result = await _service.ValidateTokenAsync(token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user123", result!.UserId);
        Assert.Equal("match456", result.MatchId);
    }

    [Fact]
    public async Task RevokeTokenAsync_DeletesTokenFromValkey()
    {
        // Arrange
        _dbMock.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RevokeTokenAsync("some-token.abc123");

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
        // Act
        var token1 = await _service.IssueTokenAsync("user1", "match1");
        var token2 = await _service.IssueTokenAsync("user2", "match2");

        // Assert
        Assert.NotEqual(token1, token2);
    }
}
