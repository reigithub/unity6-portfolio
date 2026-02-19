using Game.Server.Shared.Exceptions;
using Grpc.Core;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Filters;

/// <summary>
/// StreamingHub 用グローバルフィルター。
/// ErrorException を errorCode 付きでログ出力し握りつぶす（クライアント切断を防止）。
/// </summary>
public class ValidationExceptionHubFilter : StreamingHubFilterAttribute
{
    public override async ValueTask Invoke(StreamingHubContext context, Func<StreamingHubContext, ValueTask> next)
    {
        try
        {
            await next(context);
        }
        catch (ErrorException ex)
        {
            LogValidationError(context, ex);
            // 意図的に rethrow しない: StreamingHub で例外スローするとクライアント切断されるため
        }
    }

    protected virtual void LogValidationError(StreamingHubContext context, ErrorException ex)
    {
        var httpContext = context.ServiceContext.CallContext.GetHttpContext();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<ValidationExceptionHubFilter>>();
        logger.LogWarning(
            "Validation error [{ErrorCode}] in hub {HubMethod}: {Message}",
            ex.ErrorCode, context.ServiceContext.MethodInfo.Name, ex.Message);
    }
}
