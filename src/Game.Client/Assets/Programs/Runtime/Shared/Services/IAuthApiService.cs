using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Services
{
    /// <summary>
    /// 認証 API サービスインターフェース
    /// サーバーの認証エンドポイントとの通信を担当
    /// </summary>
    public interface IAuthApiService
    {
        UniTask<ApiResponse<LoginResponse>> GuestLoginAsync();
        UniTask<ApiResponse<LoginResponse>> EmailLoginAsync(string email, string password);
        UniTask<ApiResponse<LoginResponse>> UserIdLoginAsync(string userId, string password);
        UniTask<ApiResponse<MessageResponse>> ForgotPasswordAsync(string email);
        UniTask<ApiResponse<MessageResponse>> ResetPasswordAsync(string token, string newPassword);
        UniTask<ApiResponse<LoginResponse>> RefreshTokenAsync();
        UniTask<ApiResponse<AccountLinkResponse>> LinkEmailAsync(string email, string password);
        UniTask<ApiResponse<AccountLinkResponse>> UnlinkEmailAsync();
        UniTask<ApiResponse<UserResponse>> GetMyProfileAsync();
        UniTask<ApiResponse<TransferPasswordResponse>> IssueTransferPasswordAsync();
    }
}
