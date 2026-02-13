using System;

namespace Game.Shared.Services.Network.Models
{
    /// <summary>
    /// ネットワークエラーの種類
    /// </summary>
    public enum NetworkErrorType
    {
        /// <summary>不明なエラー</summary>
        Unknown,
        /// <summary>接続エラー（オフライン、サーバー到達不可）</summary>
        ConnectionError,
        /// <summary>タイムアウト</summary>
        Timeout,
        /// <summary>サーバーエラー（5xx）</summary>
        ServerError,
        /// <summary>クライアントエラー（4xx）</summary>
        ClientError,
        /// <summary>認証エラー（401, 403）</summary>
        AuthenticationError,
        /// <summary>レート制限（429）</summary>
        RateLimited,
        /// <summary>キャンセルされた</summary>
        Cancelled,
        /// <summary>リトライ上限到達</summary>
        RetryExhausted,
        /// <summary>サーキットブレーカーがOpen状態</summary>
        CircuitBreakerOpen,
        /// <summary>バリデーションエラー</summary>
        ValidationError
    }

    /// <summary>
    /// ネットワークエラー情報
    /// </summary>
    public sealed class NetworkError
    {
        /// <summary>
        /// エラーの種類
        /// </summary>
        public NetworkErrorType Type { get; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// HTTPステータスコード（取得できた場合）
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// サーバーからのエラーコード
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// リトライ回数
        /// </summary>
        public int RetryCount { get; }

        /// <summary>
        /// 元の例外（デバッグ用）
        /// </summary>
        public Exception InnerException { get; }

        public NetworkError(
            NetworkErrorType type,
            string message,
            int? statusCode = null,
            string errorCode = null,
            int retryCount = 0,
            Exception innerException = null)
        {
            Type = type;
            Message = message ?? GetDefaultMessage(type);
            StatusCode = statusCode;
            ErrorCode = errorCode;
            RetryCount = retryCount;
            InnerException = innerException;
        }

        /// <summary>
        /// 接続エラーを作成
        /// </summary>
        public static NetworkError ConnectionFailed(string message = null, int retryCount = 0)
            => new(NetworkErrorType.ConnectionError,
                message ?? "サーバーに接続できません",
                retryCount: retryCount);

        /// <summary>
        /// タイムアウトエラーを作成
        /// </summary>
        public static NetworkError TimedOut(int timeoutSeconds, int retryCount = 0)
            => new(NetworkErrorType.Timeout,
                $"リクエストがタイムアウトしました（{timeoutSeconds}秒）",
                retryCount: retryCount);

        /// <summary>
        /// サーバーエラーを作成
        /// </summary>
        public static NetworkError ServerFailed(int statusCode, string message = null, int retryCount = 0)
            => new(NetworkErrorType.ServerError,
                message ?? $"サーバーエラーが発生しました（{statusCode}）",
                statusCode,
                retryCount: retryCount);

        /// <summary>
        /// クライアントエラーを作成
        /// </summary>
        public static NetworkError ClientFailed(int statusCode, string message = null, string errorCode = null)
            => new(NetworkErrorType.ClientError,
                message ?? $"リクエストエラーが発生しました（{statusCode}）",
                statusCode,
                errorCode);

        /// <summary>
        /// 認証エラーを作成
        /// </summary>
        public static NetworkError AuthenticationFailed(int statusCode, string message = null)
            => new(NetworkErrorType.AuthenticationError,
                message ?? "認証に失敗しました",
                statusCode);

        /// <summary>
        /// レート制限エラーを作成
        /// </summary>
        public static NetworkError RateLimitExceeded(string message = null)
            => new(NetworkErrorType.RateLimited,
                message ?? "リクエスト制限に達しました。しばらく待ってから再試行してください",
                429);

        /// <summary>
        /// キャンセルエラーを作成
        /// </summary>
        public static NetworkError Cancelled()
            => new(NetworkErrorType.Cancelled, "リクエストがキャンセルされました");

        /// <summary>
        /// リトライ上限到達エラーを作成
        /// </summary>
        public static NetworkError RetryExhausted(int maxRetries, Exception lastError = null)
            => new(NetworkErrorType.RetryExhausted,
                $"リトライ上限（{maxRetries}回）に達しました",
                retryCount: maxRetries,
                innerException: lastError);

        /// <summary>
        /// サーキットブレーカーOpenエラーを作成
        /// </summary>
        public static NetworkError CircuitOpen(TimeSpan remainingTime)
            => new(NetworkErrorType.CircuitBreakerOpen,
                $"サーバーが一時的に利用できません。{remainingTime.TotalSeconds:F0}秒後に再試行してください");

        /// <summary>
        /// リトライ可能なエラーかどうか
        /// </summary>
        public bool IsRetryable =>
            Type == NetworkErrorType.ConnectionError ||
            Type == NetworkErrorType.Timeout ||
            Type == NetworkErrorType.ServerError ||
            Type == NetworkErrorType.RateLimited;

        /// <summary>
        /// オフラインによるエラーかどうか
        /// </summary>
        public bool IsOfflineError =>
            Type == NetworkErrorType.ConnectionError;

        public override string ToString()
        {
            var statusInfo = StatusCode.HasValue ? $" (Status: {StatusCode})" : "";
            var retryInfo = RetryCount > 0 ? $" [Retries: {RetryCount}]" : "";
            return $"[{Type}] {Message}{statusInfo}{retryInfo}";
        }

        private static string GetDefaultMessage(NetworkErrorType type)
        {
            return type switch
            {
                NetworkErrorType.Unknown => "不明なエラーが発生しました",
                NetworkErrorType.ConnectionError => "サーバーに接続できません",
                NetworkErrorType.Timeout => "リクエストがタイムアウトしました",
                NetworkErrorType.ServerError => "サーバーエラーが発生しました",
                NetworkErrorType.ClientError => "リクエストエラーが発生しました",
                NetworkErrorType.AuthenticationError => "認証に失敗しました",
                NetworkErrorType.RateLimited => "リクエスト制限に達しました",
                NetworkErrorType.Cancelled => "リクエストがキャンセルされました",
                NetworkErrorType.RetryExhausted => "リトライ上限に達しました",
                NetworkErrorType.CircuitBreakerOpen => "サーバーが一時的に利用できません",
                NetworkErrorType.ValidationError => "入力内容に誤りがあります",
                _ => "エラーが発生しました"
            };
        }
    }
}
