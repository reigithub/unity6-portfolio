using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Auth;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Models;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証 API サービス実装
    /// IApiClient を使用してサーバーの認証エンドポイントを呼び出す
    /// ログイン成功時に IAuthSessionService へ自動保存
    /// </summary>
    public class AuthApiService : IAuthApiService
    {
        private readonly IApiClient _apiClient;
        private readonly IAuthSessionService _authSessionService;

        /// <summary>
        /// 認証リクエスト用のオプション（デバイス情報ヘッダー付き）
        /// </summary>
        private static RequestOptions AuthRequestOptions =>
            RequestOptions.WithHeaders(DeviceHeaderProvider.GetAuthHeaders());

        public AuthApiService(IApiClient apiClient, IAuthSessionService authSessionService)
        {
            _apiClient = apiClient;
            _authSessionService = authSessionService;
        }

        public async UniTask<ApiResponse<LoginResponse>> GuestLoginAsync()
        {
            var fingerprint = await _authSessionService.GetOrCreateDeviceFingerprintAsync();
            var request = new GuestLoginRequest { deviceFingerprint = fingerprint };

            var response = await _apiClient.PostAsync<GuestLoginRequest, LoginResponse>(
                "api/auth/guest", request, AuthRequestOptions);

            if (response.IsSuccess)
            {
                await OnLoginSuccessAsync(response.Data, "guest");
            }

            return response;
        }

        public async UniTask<ApiResponse<LoginResponse>> EmailLoginAsync(string email, string password)
        {
            var request = new EmailLoginRequest { email = email, password = password };
            var response = await _apiClient.PostAsync<EmailLoginRequest, LoginResponse>(
                "api/auth/email/login", request, AuthRequestOptions);

            if (response.IsSuccess)
            {
                await OnLoginSuccessAsync(response.Data, "password");
            }

            return response;
        }

        public async UniTask<ApiResponse<LoginResponse>> UserIdLoginAsync(string userId, string password)
        {
            var request = new UserIdLoginRequest { userId = userId, password = password };
            var response = await _apiClient.PostAsync<UserIdLoginRequest, LoginResponse>(
                "api/auth/login", request, AuthRequestOptions);

            if (response.IsSuccess)
            {
                await OnLoginSuccessAsync(response.Data, "password");
            }

            return response;
        }

        public async UniTask<ApiResponse<MessageResponse>> ForgotPasswordAsync(string email)
        {
            var request = new ForgotPasswordRequest { email = email };
            return await _apiClient.PostAsync<ForgotPasswordRequest, MessageResponse>(
                "api/auth/email/forgot-password", request);
        }

        public async UniTask<ApiResponse<MessageResponse>> ResetPasswordAsync(
            string token, string newPassword)
        {
            var request = new ResetPasswordRequest
            {
                token = token,
                newPassword = newPassword
            };

            return await _apiClient.PostAsync<ResetPasswordRequest, MessageResponse>(
                "api/auth/email/reset-password", request);
        }

        public async UniTask<ApiResponse<LoginResponse>> RefreshTokenAsync()
        {
            // refresh は空ボディの POST（Bearer トークンで認証）
            // デバイス情報ヘッダーを付与してセキュリティ向上
            var response = await _apiClient.PostAsync<EmptyRequest, LoginResponse>(
                "api/auth/refresh", new EmptyRequest(), AuthRequestOptions);

            if (response.IsSuccess)
            {
                await OnLoginSuccessAsync(response.Data, _authSessionService.AuthType ?? "guest");
            }

            return response;
        }

        public async UniTask<ApiResponse<AccountLinkResponse>> LinkEmailAsync(
            string email, string password)
        {
            var request = new LinkEmailRequest
            {
                email = email,
                password = password
            };

            var response = await _apiClient.PostAsync<LinkEmailRequest, AccountLinkResponse>(
                "api/auth/link/email", request, AuthRequestOptions);

            if (response.IsSuccess)
            {
                await OnLinkSuccessAsync(response.Data);
            }

            return response;
        }

        public async UniTask<ApiResponse<AccountLinkResponse>> UnlinkEmailAsync()
        {
            var fingerprint = await _authSessionService.GetOrCreateDeviceFingerprintAsync();
            var response = await _apiClient.DeleteAsync<AccountLinkResponse>(
                $"api/auth/link/email?deviceFingerprint={UnityEngine.Networking.UnityWebRequest.EscapeURL(fingerprint)}");

            if (response.IsSuccess)
            {
                await OnLinkSuccessAsync(response.Data);
            }

            return response;
        }

        public async UniTask<ApiResponse<UserProfileResponse>> GetMyProfileAsync()
        {
            return await _apiClient.GetAsync<UserProfileResponse>("api/users/me");
        }

        public async UniTask<ApiResponse<TransferPasswordResponse>> IssueTransferPasswordAsync()
        {
            // 引き継ぎパスワード発行にもデバイス情報を付与
            return await _apiClient.PostAsync<EmptyRequest, TransferPasswordResponse>(
                "api/auth/transfer-password", new EmptyRequest(), AuthRequestOptions);
        }

        private async UniTask OnLoginSuccessAsync(LoginResponse data, string authType)
        {
            await _authSessionService.SaveSessionAsync(data, authType);
            _apiClient.SetAuthToken(data.token);

            if (!string.IsNullOrEmpty(data.signingKey))
            {
                _apiClient.SetSigningKey(data.signingKey);
            }
        }

        private async UniTask OnLinkSuccessAsync(AccountLinkResponse data)
        {
            var loginData = new LoginResponse
            {
                userId = data.userId,
                userName = data.userName,
                token = data.token,
                signingKey = data.signingKey
            };
            await _authSessionService.SaveSessionAsync(loginData, data.authType?.ToLower() ?? "guest");
            _apiClient.SetAuthToken(data.token);

            if (!string.IsNullOrEmpty(data.signingKey))
            {
                _apiClient.SetSigningKey(data.signingKey);
            }
        }

        /// <summary>
        /// 空リクエスト用のダミー型（refresh 用）
        /// </summary>
        [System.Serializable]
        private class EmptyRequest { }
    }
}
