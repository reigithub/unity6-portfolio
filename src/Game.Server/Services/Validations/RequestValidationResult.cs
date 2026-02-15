namespace Game.Server.Services.Validations;

public class RequestValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private RequestValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static RequestValidationResult Success() => new(true, null);
    public static RequestValidationResult Failure(string message) => new(false, message);
}
