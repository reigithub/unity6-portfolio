using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Shared.Services
{
    /// <summary>
    /// UnityWebRequest ベースの API クライアント実装
    /// GameEnvironmentConfig.ApiBaseUrl をベース URL として使用
    /// </summary>
    public class UnityApiClient : IApiClient
    {
        private const int TimeoutSeconds = 15;
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

        public async UniTask<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body)
        {
            var url = $"{BaseUrl}/{path.TrimStart('/')}";
            var jsonBody = JsonUtility.ToJson(body);
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", ContentType);
            request.timeout = TimeoutSeconds;

            SetAuthHeader(request);

            return await SendRequest<TResponse>(request);
        }

        public async UniTask<ApiResponse<TResponse>> GetAsync<TResponse>(string path)
        {
            var url = $"{BaseUrl}/{path.TrimStart('/')}";

            using var request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;

            SetAuthHeader(request);

            return await SendRequest<TResponse>(request);
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }
        }

        private async UniTask<ApiResponse<TResponse>> SendRequest<TResponse>(UnityWebRequest request)
        {
            try
            {
                await request.SendWebRequest().ToUniTask();
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
