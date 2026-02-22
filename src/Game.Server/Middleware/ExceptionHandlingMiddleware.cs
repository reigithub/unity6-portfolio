using Game.Library.Shared.Dto;
using Game.Server.Shared.Exceptions;
using MessagePack;

namespace Game.Server.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly bool _isDevelopment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
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

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            ErrorException errorEx => (errorEx.StatusCode, errorEx.ErrorCode),
            ArgumentException => (StatusCodes.Status400BadRequest, "BAD_REQUEST"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "CONFLICT"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR"),
        };

        var response = new ApiErrorResponse
        {
            Error = errorCode,
            Message = GetErrorMessage(exception, errorCode),
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

    private string GetErrorMessage(Exception exception, string errorCode)
    {
        // ErrorException は開発者が制御したメッセージなので常に返す
        if (exception is ErrorException)
        {
            return exception.Message;
        }

        // 開発環境では詳細メッセージを返す
        if (_isDevelopment)
        {
            return exception.Message;
        }

        // 本番環境ではエラーコードに対応する汎用メッセージを返す
        return errorCode switch
        {
            "BAD_REQUEST" => "The request was invalid.",
            "UNAUTHORIZED" => "Authentication is required.",
            "NOT_FOUND" => "The requested resource was not found.",
            "CONFLICT" => "The request conflicts with the current state.",
            _ => "An internal error occurred.",
        };
    }
}
