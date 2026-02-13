using System;

namespace Game.Shared.Services
{
    /// <summary>
    /// API レスポンスのラッパー
    /// 成功/失敗を統一的に扱う
    /// </summary>
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public ApiErrorResponse Error { get; set; }
        public long StatusCode { get; set; }

        /// <summary>
        /// キャッシュからのレスポンスかどうか
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// オフライン時のキャッシュフォールバックかどうか
        /// </summary>
        public bool IsOfflineFallback { get; set; }

        /// <summary>
        /// サーキットブレーカーOpen時のキャッシュフォールバックかどうか
        /// </summary>
        public bool IsCircuitOpenFallback { get; set; }

        /// <summary>
        /// キャッシュからの成功レスポンスを作成
        /// </summary>
        public static ApiResponse<T> SuccessFromCache(T data, bool isOffline = false, bool isCircuitOpen = false)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Data = data,
                StatusCode = 200,
                FromCache = true,
                IsOfflineFallback = isOffline,
                IsCircuitOpenFallback = isCircuitOpen
            };
        }

        /// <summary>
        /// オフラインエラーレスポンスを作成
        /// </summary>
        public static ApiResponse<T> OfflineError()
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse
                {
                    error = "Offline",
                    message = "ネットワークに接続されていません"
                },
                StatusCode = 0
            };
        }

        /// <summary>
        /// サーキットブレーカーOpenエラーレスポンスを作成
        /// </summary>
        public static ApiResponse<T> CircuitOpenError(TimeSpan remainingTime)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Error = new ApiErrorResponse
                {
                    error = "CircuitBreakerOpen",
                    message = $"サーバーが一時的に利用できません。{remainingTime.TotalSeconds:F0}秒後に再試行してください。"
                },
                StatusCode = 503
            };
        }
    }

    /// <summary>
    /// API エラーレスポンス（サーバーの ApiErrorResponse と対応）
    /// </summary>
    [Serializable]
    public class ApiErrorResponse
    {
        public string error;
        public string message;
        public string traceId;

        public string Error => error;
        public string Message => message;

        /// <summary>
        /// オフラインエラーかどうか
        /// </summary>
        public bool IsOfflineError => error == "Offline" || error == "ConnectionError";
    }
}
