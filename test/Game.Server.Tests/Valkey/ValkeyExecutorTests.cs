using Game.Server.Shared.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Game.Server.Tests.Valkey;

public class ValkeyExecutorTests
{
    private readonly Mock<ILogger> _loggerMock = new();

    [Fact]
    public async Task ExecuteAsync_T_ReturnsValue_WhenOperationSucceeds()
    {
        var result = await ValkeyExecutor.ExecuteAsync(
            () => Task.FromResult(42),
            fallback: 0,
            NullLogger.Instance,
            "TestOp");

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_T_ReturnsFallback_OnRedisConnectionException()
    {
        var result = await ValkeyExecutor.ExecuteAsync<int>(
            () => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"),
            fallback: -1,
            _loggerMock.Object,
            "TestOp");

        Assert.Equal(-1, result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<RedisConnectionException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_T_ReturnsFallback_OnRedisTimeoutException()
    {
        var result = await ValkeyExecutor.ExecuteAsync<string>(
            () => throw new RedisTimeoutException("timeout", CommandStatus.Unknown),
            fallback: "fallback",
            _loggerMock.Object,
            "TestOp");

        Assert.Equal("fallback", result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<RedisTimeoutException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_T_ReturnsFallback_OnUnexpectedException()
    {
        var result = await ValkeyExecutor.ExecuteAsync<int>(
            () => throw new InvalidOperationException("unexpected"),
            fallback: 99,
            _loggerMock.Object,
            "TestOp");

        Assert.Equal(99, result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Void_Completes_WhenOperationSucceeds()
    {
        var executed = false;

        await ValkeyExecutor.ExecuteAsync(
            () => { executed = true; return Task.CompletedTask; },
            NullLogger.Instance,
            "TestOp");

        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_Void_LogsWarning_OnRedisException()
    {
        await ValkeyExecutor.ExecuteAsync(
            () => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"),
            _loggerMock.Object,
            "TestOp");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<RedisConnectionException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Void_LogsError_OnUnexpectedException()
    {
        await ValkeyExecutor.ExecuteAsync(
            () => throw new InvalidOperationException("unexpected"),
            _loggerMock.Object,
            "TestOp");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
