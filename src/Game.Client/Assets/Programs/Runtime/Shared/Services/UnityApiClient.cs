using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Services.Network.Models;
using Game.Shared.Services.Network.Policies;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Shared.Services
{
    /// <summary>
    /// UnityWebRequest ベースの API クライアント実装
    /// GameEnvironmentConfig.ApiBaseUrl をベース URL として使用
    /// リトライポリシーとリクエストオプションをサポート
    /// </summary>
    public class UnityApiClient : IApiClient
    {
        private const int DefaultTimeoutSeconds = 15;
        private const string ContentType = "application/json";

        private string _authToken;

        private string BaseUrl =>
            GameEnvironmentHelper.CurrentConfig?.ApiBaseUrl?.TrimEnd('/') ?? "http://localhost:5000";

        public void SetAuthToken(string token)
        {
            _authToken = token;
        }

        public void ClearAuthToken()
        {
            _authToken = null;
        }

        public async UniTask<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string path,
            TRequest body,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;
            var retryPolicy = options.GetEffectiveRetryPolicy();
            var timeout = options.GetEffectiveTimeout(DefaultTimeoutSeconds);

            return await ExecuteWithRetry<TResponse>(
                () => CreatePostRequest(path, body, timeout, options),
                path,
                retryPolicy,
                cancellationToken);
        }

        public async UniTask<ApiResponse<TResponse>> GetAsync<TResponse>(
            string path,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;
            var retryPolicy = options.GetEffectiveRetryPolicy();
            var timeout = options.GetEffectiveTimeout(DefaultTimeoutSeconds);

            return await ExecuteWithRetry<TResponse>(
                () => CreateGetRequest(path, timeout, options),
                path,
                retryPolicy,
                cancellationToken);
        }

        public async UniTask<ApiResponse<TResponse>> DeleteAsync<TResponse>(
            string path,
            RequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= RequestOptions.Default;
            var retryPolicy = options.GetEffectiveRetryPolicy();
            var timeout = options.GetEffectiveTimeout(DefaultTimeoutSeconds);

            return await ExecuteWithRetry<TResponse>(
                () => CreateDeleteRequest(path, timeout, options),
                path,
                retryPolicy,
                cancellationToken);
        }

        private UnityWebRequest CreatePostRequest<TRequest>(string path, TRequest body, int timeout, RequestOptions options)
        {
            var url = $"{BaseUrl}/{path.TrimStart('/')}";
            var jsonBody = JsonUtility.ToJson(body);
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", ContentType);
            request.timeout = timeout;

            SetAuthHeader(request);
            SetAdditionalHeaders(request, options);

            return request;
        }

        private UnityWebRequest CreateGetRequest(string path, int timeout, RequestOptions options)
        {
            var url = $"{BaseUrl}/{path.TrimStart('/')}";

            var request = UnityWebRequest.Get(url);
            request.timeout = timeout;

            SetAuthHeader(request);
            SetAdditionalHeaders(request, options);

            return request;
        }

        private UnityWebRequest CreateDeleteRequest(string path, int timeout, RequestOptions options)
        {
            var url = $"{BaseUrl}/{path.TrimStart('/')}";

            var request = UnityWebRequest.Delete(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeout;

            SetAuthHeader(request);
            SetAdditionalHeaders(request, options);

            return request;
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }
        }

        private void SetAdditionalHeaders(UnityWebRequest request, RequestOptions options)
        {
            if (options?.AdditionalHeaders == null) return;

            foreach (var header in options.AdditionalHeaders)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        private async UniTask<ApiResponse<TResponse>> ExecuteWithRetry<TResponse>(
            Func<UnityWebRequest> requestFactory,
            string path,
            RetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            ApiResponse<TResponse> lastResponse = null;
            Exception lastException = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var request = requestFactory();

                try
                {
                    lastResponse = await SendRequest<TResponse>(request, cancellationToken);

                    // 成功した場合は即座に返す
                    if (lastResponse.IsSuccess)
                    {
                        return lastResponse;
                    }

                    // リトライ不要なエラーの場合は即座に返す
                    if (!ShouldRetry(lastResponse, retryPolicy))
                    {
                        return lastResponse;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    // 例外が発生した場合もリトライを試みる（接続エラーなど）
                }

                attempt++;

                // リトライ上限チェック
                if (!retryPolicy.CanRetry(attempt))
                {
                    if (lastResponse != null)
                    {
                        return lastResponse;
                    }

                    // レスポンスがなく例外のみの場合
                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = false,
                        Error = new ApiErrorResponse
                        {
                            error = "RetryExhausted",
                            message = $"リトライ上限（{retryPolicy.MaxRetries}回）に達しました。最後のエラー: {lastException?.Message ?? "不明"}"
                        },
                        StatusCode = 0
                    };
                }

                // 指数バックオフで待機
                var delayMs = retryPolicy.GetDelayMs(attempt - 1);
                Debug.Log($"[UnityApiClient] リトライ待機中... (attempt={attempt}, delay={delayMs}ms, path={path})");

                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
            }
        }

        private bool ShouldRetry(ApiResponse<object> response, RetryPolicy retryPolicy)
        {
            // 成功している場合はリトライ不要
            if (response.IsSuccess)
            {
                return false;
            }

            // ステータスコードがリトライ対象かチェック
            var statusCode = (int)response.StatusCode;
            return retryPolicy.IsRetryableStatusCode(statusCode);
        }

        private bool ShouldRetry<TResponse>(ApiResponse<TResponse> response, RetryPolicy retryPolicy)
        {
            // 成功している場合はリトライ不要
            if (response.IsSuccess)
            {
                return false;
            }

            // 接続エラーの場合はリトライ対象
            if (response.Error?.error == "ConnectionError")
            {
                return true;
            }

            // ステータスコードがリトライ対象かチェック
            var statusCode = (int)response.StatusCode;
            return retryPolicy.IsRetryableStatusCode(statusCode);
        }

        private async UniTask<ApiResponse<TResponse>> SendRequest<TResponse>(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityWebRequestException)
            {
                // エラーはステータスコードで判定するため、ここでは握りつぶす
            }

            var statusCode = request.responseCode;
            var responseText = request.downloadHandler?.text;

            if (request.result == UnityWebRequest.Result.Success)
            {
                return new ApiResponse<TResponse>
                {
                    IsSuccess = true,
                    Data = JsonUtility.FromJson<TResponse>(responseText),
                    StatusCode = statusCode
                };
            }

            // エラーレスポンスの解析
            ApiErrorResponse errorResponse = null;
            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    errorResponse = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                }
                catch (Exception)
                {
                    errorResponse = new ApiErrorResponse { message = responseText };
                }
            }

            // ネットワークエラー（サーバー未応答など）
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                errorResponse ??= new ApiErrorResponse
                {
                    error = "ConnectionError",
                    message = "サーバーに接続できません。ネットワーク接続を確認してください。"
                };
            }
            else if (errorResponse == null)
            {
                errorResponse = new ApiErrorResponse
                {
                    error = "UnknownError",
                    message = request.error ?? "不明なエラーが発生しました。"
                };
            }

            return new ApiResponse<TResponse>
            {
                IsSuccess = false,
                Error = errorResponse,
                StatusCode = statusCode
            };
        }
    }
}
