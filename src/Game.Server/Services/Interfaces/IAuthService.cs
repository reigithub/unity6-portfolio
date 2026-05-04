using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request);

    Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(string refreshToken);

    Task<Result<LoginResponse, ApiError>> GuestLoginAsync(GuestLoginRequest request);

    Task<Result<LoginResponse, ApiError>> EmailLoginAsync(EmailLoginRequest request);

    Task<Result<bool, ApiError>> VerifyEmailAsync(VerifyEmailRequest request);

    Task<Result<bool, ApiError>> ForgotPasswordAsync(ForgotPasswordRequest request);

    Task<Result<bool, ApiError>> ResetPasswordAsync(ResetPasswordRequest request);

    Task<Result<AccountLinkResponse, ApiError>> LinkEmailAsync(string userId, LinkEmailRequest request);

    Task<Result<AccountLinkResponse, ApiError>> UnlinkEmailAsync(string userId, string deviceFingerprint);

    Task<Result<TransferPasswordResponse, ApiError>> IssueTransferPasswordAsync(string userId);
}
