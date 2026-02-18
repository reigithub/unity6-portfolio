using Game.Server.Configuration;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Microsoft.Extensions.Options;
using Resend;

namespace Game.Server.Services;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(IResend resend, IOptions<ResendSettings> settings, ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<bool, ApiError>> SendVerificationEmailAsync(string toEmail, string token)
    {
        try
        {
            var message = new EmailMessage();
            message.From = $"{_settings.FromName} <{_settings.FromEmail}>";
            message.To.Add(toEmail);
            message.Subject = "Verify your email address";
            message.HtmlBody = $@"<h2>Email Verification</h2>
                <p>Your verification token is:</p>
                <p><strong>{token}</strong></p>
                <p>This token will expire in 24 hours.</p>";

            await _resend.EmailSendAsync(message);
            return true;
        }
        catch (ResendException ex)
        {
            _logger.LogError(ex,
                "Resend API error sending verification email to {ToEmail}: ErrorType={ErrorType}, StatusCode={StatusCode}, IsTransient={IsTransient}",
                toEmail, ex.ErrorType, ex.StatusCode, ex.IsTransient);
            return new ApiError("Failed to send verification email", "EMAIL_SEND_FAILED", StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending verification email to {ToEmail}", toEmail);
            return new ApiError("Failed to send verification email", "EMAIL_SEND_FAILED", StatusCodes.Status502BadGateway);
        }
    }

    public async Task<Result<bool, ApiError>> SendPasswordResetEmailAsync(string toEmail, string token)
    {
        try
        {
            var message = new EmailMessage();
            message.From = $"{_settings.FromName} <{_settings.FromEmail}>";
            message.To.Add(toEmail);
            message.Subject = "Reset your password";
            message.HtmlBody = $@"<h2>Password Reset</h2>
                <p>Your password reset token is:</p>
                <p><strong>{token}</strong></p>
                <p>This token will expire in 30 minutes.</p>";

            await _resend.EmailSendAsync(message);
            return true;
        }
        catch (ResendException ex)
        {
            _logger.LogError(ex,
                "Resend API error sending password reset email to {ToEmail}: ErrorType={ErrorType}, StatusCode={StatusCode}, IsTransient={IsTransient}",
                toEmail, ex.ErrorType, ex.StatusCode, ex.IsTransient);
            return new ApiError("Failed to send password reset email", "EMAIL_SEND_FAILED", StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending password reset email to {ToEmail}", toEmail);
            return new ApiError("Failed to send password reset email", "EMAIL_SEND_FAILED", StatusCodes.Status502BadGateway);
        }
    }
}
