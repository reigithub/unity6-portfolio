using Game.Server.Shared.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace Game.Server.Filters;

/// <summary>
/// SignalR Hub 用フィルター。ErrorException を HubException に変換する。
/// </summary>
public class ChatValidationFilter : IHubFilter
{
    private readonly ILogger<ChatValidationFilter> _logger;

    public ChatValidationFilter(ILogger<ChatValidationFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (ErrorException ex)
        {
            _logger.LogWarning(
                "Validation error [{ErrorCode}] in hub {HubMethod}: {Message}",
                ex.ErrorCode, invocationContext.HubMethodName, ex.Message);
            throw new HubException(ex.Message);
        }
    }
}
