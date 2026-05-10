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
        public int TimeoutSeconds { get; }

        public NetworkTimeoutException(string endpoint, int timeoutSeconds, int retryCount = 0)
            : base($"リクエストがタイムアウトしました（{timeoutSeconds}秒）", "NETWORK_TIMEOUT", 2, endpoint, null, retryCount)
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
        public NetworkConnectionException(string endpoint, int retryCount = 0)
            : base("サーバーに接続できません", "NETWORK_CONNECTION_FAILED", 2, endpoint, null, retryCount)
        {
        }

        public NetworkConnectionException(string message, string endpoint, int retryCount = 0)
            : base(message, "NETWORK_CONNECTION_FAILED", 2, endpoint, null, retryCount)
        {
        }

        public NetworkConnectionException(string message, Exception innerException, string endpoint, int retryCount = 0)
            : base(message, innerException, "NETWORK_CONNECTION_FAILED", 2, endpoint, null, retryCount)
        {
        }
    }

    /// <summary>
    /// サーバーエラー例外（5xx系）
    /// </summary>
    public class NetworkServerException : NetworkException
    {
        public string ServerMessage { get; }

        public NetworkServerException(string endpoint, int statusCode, string serverMessage = null, int retryCount = 0)
            : base($"サーバーエラーが発生しました（{statusCode}）", "NETWORK_SERVER_ERROR", 2, endpoint, statusCode, retryCount)
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
        public string ServerMessage { get; }

        public NetworkClientException(string endpoint, int statusCode, string serverMessage = null, int retryCount = 0)
            : base($"クライアントエラーが発生しました（{statusCode}）", "NETWORK_CLIENT_ERROR", 1, endpoint, statusCode, retryCount)
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
        public Exception LastError { get; }

        public NetworkRetryExhaustedException(string endpoint, int maxRetries, Exception lastError = null)
            : base($"リトライ上限（{maxRetries}回）に達しました", lastError, "NETWORK_RETRY_EXHAUSTED", 2, endpoint, null, maxRetries)
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
