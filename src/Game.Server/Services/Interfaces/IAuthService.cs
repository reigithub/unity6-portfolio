using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request);

    Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(Guid id);

    Task<Result<LoginResponse, ApiError>> GuestLoginAsync(GuestLoginRequest request);

    Task<Result<LoginResponse, ApiError>> EmailLoginAsync(EmailLoginRequest request);

    Task<Result<bool, ApiError>> VerifyEmailAsync(VerifyEmailRequest request);

    Task<Result<bool, ApiError>> ForgotPasswordAsync(ForgotPasswordRequest request);

    Task<Result<bool, ApiError>> ResetPasswordAsync(ResetPasswordRequest request);

    Task<Result<AccountLinkResponse, ApiError>> LinkEmailAsync(Guid id, LinkEmailRequest request);

    Task<Result<AccountLinkResponse, ApiError>> UnlinkEmailAsync(Guid id, string deviceFingerprint);

    Task<Result<TransferPasswordResponse, ApiError>> IssueTransferPasswordAsync(Guid id);
}
