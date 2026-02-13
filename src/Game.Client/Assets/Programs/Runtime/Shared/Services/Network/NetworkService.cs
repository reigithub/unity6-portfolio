using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Services.Network.Cache;
using Game.Shared.Services.Network.Connectivity;
using Game.Shared.Services.Network.Models;
using R3;
using UnityEngine;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// 統一ネットワークサービスの実装
    /// IApiClient、IConnectivityChecker、IResponseCacheを統合
    /// </summary>
    public class NetworkService : INetworkService, IDisposable
    {
        private readonly IApiClient _apiClient;
        private readonly IConnectivityChecker _connectivityChecker;
        private readonly IResponseCache _cache;
        private bool _isDisposed;

        public bool IsConnected => _connectivityChecker.IsConnected;
        public Observable<bool> OnConnectivityChanged => _connectivityChecker.OnConnectivityChanged;

        public NetworkService(
            IApiClient apiClient,
            IConnectivityChecker connectivityChecker,
            IResponseCache cache)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _connectivityChecker = connectivityChecker ?? throw new ArgumentNullException(nameof(connectivityChecker));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // 接続監視を開始
            _connectivityChecker.StartMonitoring();
        }

        public async UniTask<NetworkResult<T>> GetAsync<T>(
            string endpoint,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;
            var cacheKey = GetCacheKey(endpoint, options);

            // オフライン時はキャッシュフォールバック
            if (!IsConnected)
            {
                return await HandleOfflineRequest<T>(cacheKey, options);
            }

            // キャッシュチェック
            if (options.UseCache)
            {
                var cached = await TryGetFromCache<T>(cacheKey);
                if (cached.HasValue)
                {
                    return cached.Value;
                }
            }

            // APIリクエスト実行
            try
            {
                var response = await _apiClient.GetAsync<T>(endpoint, options, cancellationToken);
                var result = ConvertToNetworkResult(response);

                // 成功時はキャッシュに保存
                if (result.IsSuccess && options.UseCache)
                {
                    await SaveToCache(cacheKey, result.Data, options.CacheDuration);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                return NetworkResult<T>.Failure(NetworkError.Cancelled());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkService] GET request failed: {endpoint} - {ex.Message}");

                // フォールバックキャッシュ
                if (options.FallbackToCache)
                {
                    var fallback = await TryGetFallbackFromCache<T>(cacheKey);
                    if (fallback.HasValue)
                    {
                        return fallback.Value;
                    }
                }

                return NetworkResult<T>.Failure(
                    new NetworkError(NetworkErrorType.Unknown, ex.Message, innerException: ex));
            }
        }

        public async UniTask<NetworkResult<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest body,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;

            // POSTはオフライン時は基本的にエラー
            if (!IsConnected)
            {
                return NetworkResult<TResponse>.Offline("オフライン中はデータを送信できません");
            }

            try
            {
                var response = await _apiClient.PostAsync<TRequest, TResponse>(
                    endpoint, body, options, cancellationToken);
                return ConvertToNetworkResult(response);
            }
            catch (OperationCanceledException)
            {
                return NetworkResult<TResponse>.Failure(NetworkError.Cancelled());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkService] POST request failed: {endpoint} - {ex.Message}");
                return NetworkResult<TResponse>.Failure(
                    new NetworkError(NetworkErrorType.Unknown, ex.Message, innerException: ex));
            }
        }

        public async UniTask<NetworkResult<T>> DeleteAsync<T>(
            string endpoint,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;

            // DELETEはオフライン時は基本的にエラー
            if (!IsConnected)
            {
                return NetworkResult<T>.Offline("オフライン中は削除操作を実行できません");
            }

            try
            {
                var response = await _apiClient.DeleteAsync<T>(endpoint, options, cancellationToken);
                return ConvertToNetworkResult(response);
            }
            catch (OperationCanceledException)
            {
                return NetworkResult<T>.Failure(NetworkError.Cancelled());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkService] DELETE request failed: {endpoint} - {ex.Message}");
                return NetworkResult<T>.Failure(
                    new NetworkError(NetworkErrorType.Unknown, ex.Message, innerException: ex));
            }
        }

        public void SetAuthToken(string token)
        {
            _apiClient.SetAuthToken(token);
        }

        public void ClearAuthToken()
        {
            _apiClient.ClearAuthToken();
        }

        public async UniTask ClearCacheAsync()
        {
            await _cache.ClearAsync();
            Debug.Log("[NetworkService] Cache cleared");
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _connectivityChecker.StopMonitoring();
        }

        private string GetCacheKey(string endpoint, RequestOptions options)
        {
            var prefix = options?.CacheKeyPrefix ?? "";
            return $"{prefix}{endpoint}";
        }

        private async UniTask<NetworkResult<T>?> TryGetFromCache<T>(string cacheKey)
        {
            var entry = await _cache.GetAsync<T>(cacheKey);
            if (entry != null && !entry.IsExpired)
            {
                Debug.Log($"[NetworkService] Cache hit: {cacheKey}");
                return NetworkResult<T>.FromCacheSuccess(entry.Data);
            }
            return null;
        }

        private async UniTask<NetworkResult<T>?> TryGetFallbackFromCache<T>(string cacheKey)
        {
            // フォールバック時は期限切れでも使用
            var entry = await _cache.GetAsync<T>(cacheKey);
            if (entry != null)
            {
                Debug.Log($"[NetworkService] Cache fallback: {cacheKey} (expired: {entry.IsExpired})");
                return NetworkResult<T>.FromCacheSuccess(entry.Data, isOffline: !IsConnected);
            }
            return null;
        }

        private async UniTask SaveToCache<T>(string cacheKey, T data, TimeSpan? duration)
        {
            await _cache.SetAsync(cacheKey, data, duration);
            Debug.Log($"[NetworkService] Cached: {cacheKey}");
        }

        private async UniTask<NetworkResult<T>> HandleOfflineRequest<T>(string cacheKey, RequestOptions options)
        {
            Debug.Log($"[NetworkService] Offline - attempting cache fallback: {cacheKey}");

            if (options.FallbackToCache || options.UseCache)
            {
                var fallback = await TryGetFallbackFromCache<T>(cacheKey);
                if (fallback.HasValue)
                {
                    return fallback.Value;
                }
            }

            return NetworkResult<T>.Offline();
        }

        private NetworkResult<T> ConvertToNetworkResult<T>(ApiResponse<T> response)
        {
            if (response.IsSuccess)
            {
                return NetworkResult<T>.Success(response.Data, response.StatusCode);
            }

            var error = CreateNetworkError(response);
            return NetworkResult<T>.Failure(error, response.StatusCode);
        }

        private NetworkError CreateNetworkError<T>(ApiResponse<T> response)
        {
            var statusCode = (int)response.StatusCode;
            var errorCode = response.Error?.Error;
            var message = response.Error?.Message;

            // 接続エラー
            if (errorCode == "ConnectionError")
            {
                return NetworkError.ConnectionFailed(message);
            }

            // ステータスコードに基づくエラー分類
            return statusCode switch
            {
                401 or 403 => NetworkError.AuthenticationFailed(statusCode, message),
                429 => NetworkError.RateLimitExceeded(message),
                >= 500 => NetworkError.ServerFailed(statusCode, message),
                >= 400 => NetworkError.ClientFailed(statusCode, message, errorCode),
                _ => new NetworkError(NetworkErrorType.Unknown, message ?? "不明なエラー", statusCode)
            };
        }
    }
}
