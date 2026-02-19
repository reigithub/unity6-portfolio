using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Authentication;

namespace Game.Realtime.Filters;

/// <summary>
/// StreamingHub 用グローバルフィルター: JWT 認証を検証する
/// 認証失敗時は ReturnStatusException(Unauthenticated) をスローしクライアントを切断する
/// </summary>
public class JwtAuthenticationHubFilter : StreamingHubFilterAttribute
{
    public override async ValueTask Invoke(StreamingHubContext context, Func<StreamingHubContext, ValueTask> next)
    {
        var httpContext = GetHttpContext(context);

        var authResult = await AuthenticateAsync(httpContext);

        if (!authResult.Succeeded)
        {
            LogAuthenticationFailure(context, httpContext);

            throw new ReturnStatusException(
                StatusCode.Unauthenticated,
                "Authentication required. Provide a valid JWT token.");
        }

        httpContext.User = authResult.Principal!;

        await next(context);
    }

    protected virtual HttpContext GetHttpContext(StreamingHubContext context)
    {
        return context.ServiceContext.CallContext.GetHttpContext();
    }

    protected virtual ValueTask<AuthenticateResult> AuthenticateAsync(HttpContext httpContext)
    {
        return new ValueTask<AuthenticateResult>(httpContext.AuthenticateAsync());
    }

    protected virtual void LogAuthenticationFailure(StreamingHubContext context, HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<JwtAuthenticationHubFilter>>();
        logger.LogWarning(
            "Unauthenticated StreamingHub connection from {RemoteIp}",
            httpContext.Connection.RemoteIpAddress);
    }
}
