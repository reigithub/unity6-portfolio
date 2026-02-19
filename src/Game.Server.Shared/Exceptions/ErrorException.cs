namespace Game.Server.Shared.Exceptions;

/// <summary>
/// サーバー共通のエラー例外基底クラス。errorCode でエラー種別を構造化する。
/// </summary>
public class ErrorException : Exception
{
    public string ErrorCode { get; }

    public int StatusCode { get; }

    public ErrorException(string errorCode, string message, int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
