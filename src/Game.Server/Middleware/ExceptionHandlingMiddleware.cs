using Game.Library.Shared.Dto;
using MessagePack;

namespace Game.Server.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "BAD_REQUEST"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "CONFLICT"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR"),
        };

        var response = new ApiErrorResponse
        {
            Error = errorCode,
            Message = exception.Message,
            TraceId = context.TraceIdentifier,
        };

        context.Response.StatusCode = statusCode;

        var accept = context.Request.Headers.Accept.ToString();
        if (accept.Contains("application/x-msgpack"))
        {
            context.Response.ContentType = "application/x-msgpack";
            var bytes = MessagePackSerializer.Serialize(response);
            return context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(response);
    }
}
