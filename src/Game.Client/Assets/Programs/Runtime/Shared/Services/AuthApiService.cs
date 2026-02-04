using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Auth;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証 API サービス実装
    /// IApiClient を使用してサーバーの認証エンドポイントを呼び出す
    /// ログイン成功時に ISessionService へ自動保存
    /// </summary>
    public class AuthApiService : IAuthApiService
    {
        private readonly IApiClient _apiClient;
        private readonly ISessionService _sessionService;

        public AuthApiService(IApiClient apiClient, ISessionService sessionService)
        {
            _apiClient = apiClient;
            _sessionService = sessionService;
        }

        public async UniTask<ApiResponse<LoginResponse>> GuestLoginAsync()
        {
            var fingerprint = _sessionService.GetOrCreateDeviceFingerprint();
            var request = new GuestLoginRequest { deviceFingerprint = fingerprint };

            var response = await _apiClient.PostAsync<GuestLoginRequest, LoginResponse>(
                "api/auth/guest", request);

            if (response.IsSuccess)
            {
                OnLoginSuccess(response.Data, "guest");
            }

            return response;
        }

        public async UniTask<ApiResponse<LoginResponse>> UserIdLoginAsync(string userId, string password)
        {
            var request = new UserIdLoginRequest { userId = userId, password = password };
            var response = await _apiClient.PostAsync<UserIdLoginRequest, LoginResponse>(
                "api/auth/login", request);

            if (response.IsSuccess)
            {
                OnLoginSuccess(response.Data, "password");
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
            var response = await _apiClient.PostAsync<EmptyRequest, LoginResponse>(
                "api/auth/refresh", new EmptyRequest());

            if (response.IsSuccess)
            {
                OnLoginSuccess(response.Data, _sessionService.AuthType ?? "guest");
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
                "api/auth/link/email", request);

            if (response.IsSuccess)
            {
                OnLinkSuccess(response.Data);
            }

            return response;
        }

        public async UniTask<ApiResponse<AccountLinkResponse>> UnlinkEmailAsync()
        {
            var fingerprint = _sessionService.GetOrCreateDeviceFingerprint();
            var response = await _apiClient.DeleteAsync<AccountLinkResponse>(
                $"api/auth/link/email?deviceFingerprint={UnityEngine.Networking.UnityWebRequest.EscapeURL(fingerprint)}");

            if (response.IsSuccess)
            {
                OnLinkSuccess(response.Data);
            }

            return response;
        }

        public async UniTask<ApiResponse<UserProfileResponse>> GetMyProfileAsync()
        {
            return await _apiClient.GetAsync<UserProfileResponse>("api/users/me");
        }

        private void OnLoginSuccess(LoginResponse data, string authType)
        {
            _sessionService.SaveSession(data, authType);
            _apiClient.SetAuthToken(data.token);
        }

        private void OnLinkSuccess(AccountLinkResponse data)
        {
            var loginData = new LoginResponse
            {
                userId = data.userId,
                userName = data.userName,
                token = data.token
            };
            _sessionService.SaveSession(loginData, data.authType?.ToLower() ?? "guest");
            _apiClient.SetAuthToken(data.token);
        }

        /// <summary>
        /// 空リクエスト用のダミー型（refresh 用）
        /// </summary>
        [System.Serializable]
        private class EmptyRequest { }
    }
}
