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
    }
}
