using Game.Realtime.Filters;
using Game.Server.Shared.Exceptions;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Tests.Filters;

public class ValidationExceptionHubFilterTests
{
    /// <summary>
    /// StreamingHubContext は sealed でモック不可のため、ロギングをスキップするテスト用サブクラス
    /// </summary>
    private class TestableFilter : ValidationExceptionHubFilter
    {
        public ErrorException? CapturedError { get; private set; }

        protected override void LogValidationError(StreamingHubContext context, ErrorException ex)
        {
            CapturedError = ex;
        }
    }

    [Fact]
    public async Task Invoke_Success_PassesThrough()
    {
        var filter = new TestableFilter();
        var called = false;
        Func<StreamingHubContext, ValueTask> next = _ =>
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
        Func<StreamingHubContext, ValueTask> next = _ =>
            throw new InvalidOperationException("other error");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => filter.Invoke(null!, next).AsTask());
    }

    [Fact]
    public async Task Invoke_ErrorException_IsSuppressed()
    {
        var filter = new TestableFilter();
        Func<StreamingHubContext, ValueTask> next = _ =>
            throw new ErrorException("INVALID_INPUT", "test validation message");

        // ErrorException は握りつぶされる（クライアント切断防止）
        await filter.Invoke(null!, next);

        Assert.NotNull(filter.CapturedError);
        Assert.Equal("INVALID_INPUT", filter.CapturedError!.ErrorCode);
        Assert.Equal("test validation message", filter.CapturedError.Message);
    }
}
