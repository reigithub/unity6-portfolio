using Game.Server.Shared.Extensions;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authentication;

namespace Game.Realtime.Filters;

/// <summary>
/// MagicOnion グローバルフィルター: JWT 認証を検証する
/// StreamingHub 接続時に HttpContext の認証状態をチェック
/// </summary>
public class JwtAuthenticationFilter : MagicOnionFilterAttribute
{
    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        var httpContext = context.CallContext.GetHttpContext();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<JwtAuthenticationFilter>>();

        // 認証結果を取得
        var authResult = await httpContext.AuthenticateAsync();

        if (!authResult.Succeeded)
        {
            logger.LogWarning(
                "Unauthenticated gRPC request from {RemoteIp}",
                httpContext.Connection.RemoteIpAddress);

            throw new ReturnStatusException(
                StatusCode.Unauthenticated,
                "Authentication required. Provide a valid JWT token.");
        }

        // 認証済みユーザー情報をセット
        httpContext.User = authResult.Principal!;

        var userId = httpContext.User.GetUserId() ?? "unknown";
        logger.LogDebug("Authenticated gRPC request from user {UserId}", userId);

        await next(context);
    }
}
