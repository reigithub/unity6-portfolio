using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Dto.Responses;
public class ApiErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;
}

public class ApiError
{
    public ApiError(string message, string errorCode, int statusCode)
    {
        Message = message;
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string Message { get; }

    public string ErrorCode { get; }

    public int StatusCode { get; }

    public IActionResult ToActionResult()
    {
        var response = new ApiErrorResponse
        {
            Error = ErrorCode,
            Message = Message,
        };

        return new ObjectResult(response) { StatusCode = StatusCode };
    }
}

public readonly struct Result<TSuccess, TError>
{
    private readonly TSuccess? _success;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    private Result(TSuccess success)
    {
        _success = success;
        _error = default;
        _isSuccess = true;
    }

    private Result(TError error)
    {
        _success = default;
        _error = error;
        _isSuccess = false;
    }

    public static implicit operator Result<TSuccess, TError>(TSuccess success) => new(success);

    public static implicit operator Result<TSuccess, TError>(TError error) => new(error);

    public IActionResult Match(
        Func<TSuccess, IActionResult> onSuccess,
        Func<TError, IActionResult> onError)
    {
        return _isSuccess ? onSuccess(_success!) : onError(_error!);
    }
}
