using System.Security.Claims;
using Game.Realtime.Filters;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Game.Realtime.Tests.Filters;

public class JwtAuthenticationHubFilterTests
{
    /// <summary>
    /// StreamingHubContext は sealed でモック不可のため、認証とロギングをオーバーライドするテスト用サブクラス
    /// </summary>
    private class TestableFilter : JwtAuthenticationHubFilter
    {
        private readonly bool _authenticated;

        public TestableFilter(bool authenticated) => _authenticated = authenticated;

        protected override HttpContext GetHttpContext(StreamingHubContext context) => new DefaultHttpContext();

        protected override ValueTask<AuthenticateResult> AuthenticateAsync(HttpContext httpContext)
        {
            if (_authenticated)
            {
                var claims = new[] { new Claim("sub", "test-user-123") };
                var identity = new ClaimsIdentity(claims, "Bearer");
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, "Bearer");
                return new ValueTask<AuthenticateResult>(AuthenticateResult.Success(ticket));
            }

            return new ValueTask<AuthenticateResult>(
                AuthenticateResult.Fail("Test authentication failure"));
        }

        protected override void LogAuthenticationFailure(
            StreamingHubContext context, HttpContext httpContext)
        {
            // No-op: sealed StreamingHubContext の使用を回避
        }
    }

    [Fact]
    public async Task Invoke_AuthSuccess_CallsNext()
    {
        var filter = new TestableFilter(authenticated: true);
        var called = false;
        Func<StreamingHubContext, ValueTask> next = _ =>
        {
            called = true;
            return default;
        };

        await filter.Invoke(null!, next);

        Assert.True(called);
    }

    [Fact]
    public async Task Invoke_AuthFailure_ThrowsUnauthenticated()
    {
        var filter = new TestableFilter(authenticated: false);
        var nextCalled = false;
        Func<StreamingHubContext, ValueTask> next = _ =>
        {
            nextCalled = true;
            return default;
        };

        var ex = await Assert.ThrowsAsync<ReturnStatusException>(
            () => filter.Invoke(null!, next).AsTask());

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.False(nextCalled);
    }
}
