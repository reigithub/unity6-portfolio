using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse, ApiError>> RegisterAsync(RegisterRequest request);

    Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request);

    Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(string userId);

    Task<Result<LoginResponse, ApiError>> GuestLoginAsync(GuestLoginRequest request);

    Task<Result<LoginResponse, ApiError>> EmailRegisterAsync(EmailRegisterRequest request);

    Task<Result<LoginResponse, ApiError>> EmailLoginAsync(EmailLoginRequest request);

    Task<Result<bool, ApiError>> VerifyEmailAsync(VerifyEmailRequest request);

    Task<Result<bool, ApiError>> ForgotPasswordAsync(ForgotPasswordRequest request);

    Task<Result<bool, ApiError>> ResetPasswordAsync(ResetPasswordRequest request);

    Task<Result<AccountLinkResponse, ApiError>> LinkEmailAsync(string userId, LinkEmailRequest request);

    Task<Result<AccountLinkResponse, ApiError>> UnlinkEmailAsync(string userId, string deviceFingerprint);
}
