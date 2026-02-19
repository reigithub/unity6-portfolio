using System.Reflection;
using Game.Server.Filters;
using Game.Server.Shared.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Filters;

public class ChatValidationFilterTests
{
    private readonly Mock<ILogger<ChatValidationFilter>> _loggerMock;
    private readonly ChatValidationFilter _filter;

    public ChatValidationFilterTests()
    {
        _loggerMock = new Mock<ILogger<ChatValidationFilter>>();
        _filter = new ChatValidationFilter(_loggerMock.Object);
    }

    private class StubHub : Hub { }

    private static HubInvocationContext CreateMinimalContext()
    {
        var methodInfo = typeof(StubHub).GetMethod(nameof(ToString))!;
        return new HubInvocationContext(
            Mock.Of<HubCallerContext>(),
            Mock.Of<IServiceProvider>(),
            new StubHub(),
            methodInfo,
            Array.Empty<object?>());
    }

    [Fact]
    public async Task InvokeMethodAsync_Success_ReturnsResult()
    {
        var expected = "result";
        Func<HubInvocationContext, ValueTask<object?>> next = _ =>
            new ValueTask<object?>(expected);

        var result = await _filter.InvokeMethodAsync(null!, next);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task InvokeMethodAsync_NonErrorException_Propagates()
    {
        Func<HubInvocationContext, ValueTask<object?>> next = _ =>
            throw new InvalidOperationException("other error");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _filter.InvokeMethodAsync(null!, next).AsTask());
    }

    [Fact]
    public async Task InvokeMethodAsync_ErrorException_ConvertsToHubException()
    {
        var context = CreateMinimalContext();
        Func<HubInvocationContext, ValueTask<object?>> next = _ =>
            throw new ErrorException("TEST_CODE", "test validation message");

        var ex = await Assert.ThrowsAsync<HubException>(
            () => _filter.InvokeMethodAsync(context, next).AsTask());

        Assert.Equal("test validation message", ex.Message);
    }
}
