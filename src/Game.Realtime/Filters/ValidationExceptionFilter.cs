using Game.Server.Shared.Exceptions;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;

namespace Game.Realtime.Filters;

/// <summary>
/// Unary Service 用グローバルフィルター。
/// ErrorException を ReturnStatusException(InvalidArgument) に変換し、errorCode をログ出力する。
/// </summary>
public class ValidationExceptionFilter : MagicOnionFilterAttribute
{
    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        try
        {
            await next(context);
        }
        catch (ErrorException ex)
        {
            LogValidationError(context, ex);
            throw new ReturnStatusException(StatusCode.InvalidArgument, ex.Message);
        }
    }

    protected virtual void LogValidationError(ServiceContext context, ErrorException ex)
    {
        var httpContext = context.CallContext.GetHttpContext();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<ValidationExceptionFilter>>();
        logger.LogWarning(
            "Validation error [{ErrorCode}] in {ServiceMethod}: {Message}",
            ex.ErrorCode, context.MethodInfo.Name, ex.Message);
    }
}
