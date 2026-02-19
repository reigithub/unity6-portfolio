using Game.Realtime.Filters;
using Game.Server.Shared.Exceptions;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;

namespace Game.Realtime.Tests.Filters;

public class ValidationExceptionFilterTests
{
    /// <summary>
    /// ServiceContext は sealed でモック不可のため、ロギングをスキップするテスト用サブクラス
    /// </summary>
    private class TestableFilter : ValidationExceptionFilter
    {
        public ErrorException? CapturedError { get; private set; }

        protected override void LogValidationError(ServiceContext context, ErrorException ex)
        {
            CapturedError = ex;
        }
    }

    [Fact]
    public async Task Invoke_Success_PassesThrough()
    {
        var filter = new TestableFilter();
        var called = false;
        Func<ServiceContext, ValueTask> next = _ =>
        {
            called = true;
            return default;
        };

        await filter.Invoke(null!, next);

        Assert.True(called);
        Assert.Null(filter.CapturedError);
    }

    [Fact]
    public async Task Invoke_NonErrorException_Propagates()
    {
        var filter = new TestableFilter();
        Func<ServiceContext, ValueTask> next = _ =>
            throw new InvalidOperationException("other error");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => filter.Invoke(null!, next).AsTask());
    }

    [Fact]
    public async Task Invoke_ErrorException_ConvertsToReturnStatusException()
    {
        var filter = new TestableFilter();
        Func<ServiceContext, ValueTask> next = _ =>
            throw new ErrorException("INVALID_INPUT", "test validation message");

        var ex = await Assert.ThrowsAsync<ReturnStatusException>(
            () => filter.Invoke(null!, next).AsTask());

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.Contains("test validation message", ex.Detail);
        Assert.NotNull(filter.CapturedError);
        Assert.Equal("INVALID_INPUT", filter.CapturedError!.ErrorCode);
    }
}
