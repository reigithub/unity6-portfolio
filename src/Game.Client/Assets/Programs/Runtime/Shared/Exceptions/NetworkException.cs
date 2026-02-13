using System;

namespace Game.Shared.Exceptions
{
    /// <summary>
    /// ネットワーク通信に関する例外の基底クラス
    /// </summary>
    public class NetworkException : GameException
    {
        private const string DefaultErrorCode = "NETWORK_ERROR";
        private const int DefaultErrorLevel = 2; // Error

        /// <summary>
        /// リクエスト先のエンドポイント
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// HTTPステータスコード（取得できた場合）
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// リトライ回数
        /// </summary>
        public int RetryCount { get; }

        public NetworkException(string message, string endpoint = null, int? statusCode = null, int retryCount = 0)
            : base(message, DefaultErrorCode, DefaultErrorLevel)
        {
            Endpoint = endpoint;
            StatusCode = statusCode;
            RetryCount = retryCount;
        }

        public NetworkException(string message, Exception innerException, string endpoint = null, int? statusCode = null, int retryCount = 0)
            : base(message, innerException, DefaultErrorCode, DefaultErrorLevel)
        {
            Endpoint = endpoint;
            StatusCode = statusCode;
            RetryCount = retryCount;
        }

        protected NetworkException(string message, string errorCode, int errorLevel, string endpoint = null, int? statusCode = null, int retryCount = 0)
            : base(message, errorCode, errorLevel)
        {
            Endpoint = endpoint;
            StatusCode = statusCode;
            RetryCount = retryCount;
        }

        protected NetworkException(string message, Exception innerException, string errorCode, int errorLevel, string endpoint = null, int? statusCode = null, int retryCount = 0)
            : base(message, innerException, errorCode, errorLevel)
        {
            Endpoint = endpoint;
            StatusCode = statusCode;
            RetryCount = retryCount;
        }

        public override string ToString()
        {
            var statusInfo = StatusCode.HasValue ? $", StatusCode={StatusCode}" : "";
            return $"[{ErrorCode}] {Message} (Endpoint={Endpoint}{statusInfo}, RetryCount={RetryCount})";
        }
    }

    /// <summary>
    /// 接続タイムアウト例外
    /// </summary>
    public class NetworkTimeoutException : NetworkException
    {
        private const string ErrorCode = "NETWORK_TIMEOUT";
        private const int ErrorLevel = 2;

        /// <summary>
        /// タイムアウト秒数
        /// </summary>
        public int TimeoutSeconds { get; }

        public NetworkTimeoutException(string endpoint, int timeoutSeconds, int retryCount = 0)
            : base($"リクエストがタイムアウトしました（{timeoutSeconds}秒）", ErrorCode, ErrorLevel, endpoint, null, retryCount)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        public override string ToString()
        {
            return $"[{base.ErrorCode}] {Message} (Endpoint={Endpoint}, Timeout={TimeoutSeconds}s, RetryCount={RetryCount})";
        }
    }

    /// <summary>
    /// 接続不可例外（オフライン時など）
    /// </summary>
    public class NetworkConnectionException : NetworkException
    {
        private const string ErrorCode = "NETWORK_CONNECTION_FAILED";
        private const int ErrorLevel = 2;

        public NetworkConnectionException(string endpoint, int retryCount = 0)
            : base("サーバーに接続できません", ErrorCode, ErrorLevel, endpoint, null, retryCount)
        {
        }

        public NetworkConnectionException(string message, string endpoint, int retryCount = 0)
            : base(message, ErrorCode, ErrorLevel, endpoint, null, retryCount)
        {
        }

        public NetworkConnectionException(string message, Exception innerException, string endpoint, int retryCount = 0)
            : base(message, innerException, ErrorCode, ErrorLevel, endpoint, null, retryCount)
        {
        }
    }

    /// <summary>
    /// サーバーエラー例外（5xx系）
    /// </summary>
    public class NetworkServerException : NetworkException
    {
        private const string ErrorCode = "NETWORK_SERVER_ERROR";
        private const int ErrorLevel = 2;

        /// <summary>
        /// サーバーからのエラーメッセージ
        /// </summary>
        public string ServerMessage { get; }

        public NetworkServerException(string endpoint, int statusCode, string serverMessage = null, int retryCount = 0)
            : base($"サーバーエラーが発生しました（{statusCode}）", ErrorCode, ErrorLevel, endpoint, statusCode, retryCount)
        {
            ServerMessage = serverMessage;
        }

        public override string ToString()
        {
            var serverMsgInfo = !string.IsNullOrEmpty(ServerMessage) ? $", ServerMessage={ServerMessage}" : "";
            return $"[{base.ErrorCode}] {Message} (Endpoint={Endpoint}, StatusCode={StatusCode}{serverMsgInfo}, RetryCount={RetryCount})";
        }
    }

    /// <summary>
    /// クライアントエラー例外（4xx系）
    /// </summary>
    public class NetworkClientException : NetworkException
    {
        private const string ErrorCode = "NETWORK_CLIENT_ERROR";
        private const int ErrorLevel = 1; // Warning（クライアント側の問題のため）

        /// <summary>
        /// サーバーからのエラーメッセージ
        /// </summary>
        public string ServerMessage { get; }

        public NetworkClientException(string endpoint, int statusCode, string serverMessage = null, int retryCount = 0)
            : base($"クライアントエラーが発生しました（{statusCode}）", ErrorCode, ErrorLevel, endpoint, statusCode, retryCount)
        {
            ServerMessage = serverMessage;
        }

        public override string ToString()
        {
            var serverMsgInfo = !string.IsNullOrEmpty(ServerMessage) ? $", ServerMessage={ServerMessage}" : "";
            return $"[{base.ErrorCode}] {Message} (Endpoint={Endpoint}, StatusCode={StatusCode}{serverMsgInfo}, RetryCount={RetryCount})";
        }
    }

    /// <summary>
    /// リトライ上限到達例外
    /// </summary>
    public class NetworkRetryExhaustedException : NetworkException
    {
        private const string ErrorCode = "NETWORK_RETRY_EXHAUSTED";
        private const int ErrorLevel = 2;

        /// <summary>
        /// 最後に発生したエラー
        /// </summary>
        public Exception LastError { get; }

        public NetworkRetryExhaustedException(string endpoint, int maxRetries, Exception lastError = null)
            : base($"リトライ上限（{maxRetries}回）に達しました", lastError, ErrorCode, ErrorLevel, endpoint, null, maxRetries)
        {
            LastError = lastError;
        }

        public override string ToString()
        {
            var lastErrorInfo = LastError != null ? $", LastError={LastError.Message}" : "";
            return $"[{base.ErrorCode}] {Message} (Endpoint={Endpoint}, RetryCount={RetryCount}{lastErrorInfo})";
        }
    }
}
