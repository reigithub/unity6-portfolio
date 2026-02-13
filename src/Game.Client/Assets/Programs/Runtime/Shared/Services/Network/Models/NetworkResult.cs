namespace Game.Shared.Services.Network.Models
{
    /// <summary>
    /// ネットワークリクエストの結果
    /// 成功/失敗を統一的に扱う
    /// </summary>
    /// <typeparam name="T">レスポンスデータの型</typeparam>
    public readonly struct NetworkResult<T>
    {
        /// <summary>
        /// リクエストが成功したかどうか
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// レスポンスデータ（成功時のみ有効）
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// エラー情報（失敗時のみ有効）
        /// </summary>
        public NetworkError Error { get; }

        /// <summary>
        /// キャッシュからのレスポンスかどうか
        /// </summary>
        public bool FromCache { get; }

        /// <summary>
        /// オフライン状態でのレスポンスかどうか
        /// </summary>
        public bool IsOffline { get; }

        /// <summary>
        /// HTTPステータスコード（取得できた場合）
        /// </summary>
        public long StatusCode { get; }

        private NetworkResult(
            bool isSuccess,
            T data,
            NetworkError error,
            bool fromCache,
            bool isOffline,
            long statusCode)
        {
            IsSuccess = isSuccess;
            Data = data;
            Error = error;
            FromCache = fromCache;
            IsOffline = isOffline;
            StatusCode = statusCode;
        }

        /// <summary>
        /// 成功結果を作成
        /// </summary>
        public static NetworkResult<T> Success(T data, long statusCode = 200, bool fromCache = false)
        {
            return new NetworkResult<T>(
                isSuccess: true,
                data: data,
                error: null,
                fromCache: fromCache,
                isOffline: false,
                statusCode: statusCode);
        }

        /// <summary>
        /// キャッシュからの成功結果を作成
        /// </summary>
        public static NetworkResult<T> FromCacheSuccess(T data, bool isOffline = false)
        {
            return new NetworkResult<T>(
                isSuccess: true,
                data: data,
                error: null,
                fromCache: true,
                isOffline: isOffline,
                statusCode: 200);
        }

        /// <summary>
        /// 失敗結果を作成
        /// </summary>
        public static NetworkResult<T> Failure(NetworkError error, long statusCode = 0)
        {
            return new NetworkResult<T>(
                isSuccess: false,
                data: default,
                error: error,
                fromCache: false,
                isOffline: error?.IsOfflineError ?? false,
                statusCode: statusCode);
        }

        /// <summary>
        /// オフラインエラーを作成
        /// </summary>
        public static NetworkResult<T> Offline(string message = null)
        {
            return new NetworkResult<T>(
                isSuccess: false,
                data: default,
                error: NetworkError.ConnectionFailed(message ?? "オフラインです"),
                fromCache: false,
                isOffline: true,
                statusCode: 0);
        }

        /// <summary>
        /// 結果を別の型に変換
        /// </summary>
        public NetworkResult<TNew> Map<TNew>(System.Func<T, TNew> mapper)
        {
            if (!IsSuccess)
            {
                return NetworkResult<TNew>.Failure(Error, StatusCode);
            }

            return new NetworkResult<TNew>(
                isSuccess: true,
                data: mapper(Data),
                error: null,
                fromCache: FromCache,
                isOffline: IsOffline,
                statusCode: StatusCode);
        }

        /// <summary>
        /// 成功時にアクションを実行
        /// </summary>
        public NetworkResult<T> OnSuccess(System.Action<T> action)
        {
            if (IsSuccess)
            {
                action?.Invoke(Data);
            }
            return this;
        }

        /// <summary>
        /// 失敗時にアクションを実行
        /// </summary>
        public NetworkResult<T> OnFailure(System.Action<NetworkError> action)
        {
            if (!IsSuccess)
            {
                action?.Invoke(Error);
            }
            return this;
        }

        public override string ToString()
        {
            if (IsSuccess)
            {
                var cacheInfo = FromCache ? " (from cache)" : "";
                return $"Success{cacheInfo}: {Data}";
            }
            return $"Failure: {Error}";
        }
    }
}
