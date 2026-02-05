using System.Security.Claims;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }

    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.RefreshTokenAsync(userId);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }

    [HttpPost("guest")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuestLogin([FromBody] GuestLoginRequest request)
    {
        var result = await _authService.GuestLoginAsync(request);

        return result.Match(
            success => success.IsNewUser
                ? StatusCode(StatusCodes.Status201Created, success)
                : Ok(success),
            error => error.ToActionResult());
    }

    [HttpPost("email/login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EmailLogin([FromBody] EmailLoginRequest request)
    {
        var result = await _authService.EmailLoginAsync(request);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }

    [HttpPost("email/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _authService.VerifyEmailAsync(request);

        return result.Match(
            success => Ok(new { message = "Email verified successfully" }),
            error => error.ToActionResult());
    }

    [HttpPost("email/forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);

        return result.Match(
            success => Ok(new { message = "If the email exists, a reset link has been sent" }),
            error => error.ToActionResult());
    }

    [HttpPost("email/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);

        return result.Match(
            success => Ok(new { message = "Password has been reset successfully" }),
            error => error.ToActionResult());
    }

    [HttpPost("link/email")]
    [Authorize]
    [ProducesResponseType(typeof(AccountLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkEmail([FromBody] LinkEmailRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.LinkEmailAsync(userId, request);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }

    [HttpDelete("link/email")]
    [Authorize]
    [ProducesResponseType(typeof(AccountLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkEmail([FromQuery] string deviceFingerprint)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.UnlinkEmailAsync(userId, deviceFingerprint);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }

    [HttpPost("transfer-password")]
    [Authorize]
    [ProducesResponseType(typeof(TransferPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IssueTransferPassword()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.IssueTransferPasswordAsync(userId);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }
}
