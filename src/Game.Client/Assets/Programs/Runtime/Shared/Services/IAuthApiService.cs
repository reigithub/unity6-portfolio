using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Auth;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証 API サービスインターフェース
    /// サーバーの認証エンドポイントとの通信を担当
    /// </summary>
    public interface IAuthApiService
    {
        UniTask<ApiResponse<LoginResponse>> GuestLoginAsync();
        UniTask<ApiResponse<LoginResponse>> EmailRegisterAsync(string email, string password, string displayName);
        UniTask<ApiResponse<LoginResponse>> EmailLoginAsync(string email, string password);
        UniTask<ApiResponse<MessageResponse>> VerifyEmailAsync(string token);
        UniTask<ApiResponse<MessageResponse>> ForgotPasswordAsync(string email);
        UniTask<ApiResponse<MessageResponse>> ResetPasswordAsync(string token, string newPassword);
        UniTask<ApiResponse<LoginResponse>> RefreshTokenAsync();
        UniTask<ApiResponse<AccountLinkResponse>> LinkEmailAsync(string email, string password, string displayName);
        UniTask<ApiResponse<AccountLinkResponse>> UnlinkEmailAsync();
        UniTask<ApiResponse<UserProfileResponse>> GetMyProfileAsync();
    }
}
