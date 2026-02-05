using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IEmailService
{
    Task<Result<bool, ApiError>> SendVerificationEmailAsync(string toEmail, string token);

    Task<Result<bool, ApiError>> SendPasswordResetEmailAsync(string toEmail, string token);
}
