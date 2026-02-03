using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Game.Server.Configuration;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;
using Game.Server.Validation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly JwtSettings _jwtSettings;
    private readonly AuthSettings _authSettings;
    private readonly IEmailService _emailService;

    public AuthService(
        IAuthRepository authRepository,
        IOptions<JwtSettings> jwtSettings,
        IOptions<AuthSettings> authSettings,
        IEmailService emailService)
    {
        _authRepository = authRepository;
        _jwtSettings = jwtSettings.Value;
        _authSettings = authSettings.Value;
        _emailService = emailService;
    }

    public async Task<Result<LoginResponse, ApiError>> RegisterAsync(RegisterRequest request)
    {
        var (isValid, errorMessage) = PasswordValidator.Validate(request.Password);
        if (!isValid)
        {
            return new ApiError(errorMessage!, "WEAK_PASSWORD", StatusCodes.Status400BadRequest);
        }

        bool exists = await _authRepository.ExistsByDisplayNameAsync(request.DisplayName);

        if (exists)
        {
            return new ApiError("DisplayName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
        }

        var user = new UserInfo
        {
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AuthType = "Password",
        };

        await _authRepository.CreateUserAsync(user);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request)
    {
        var user = await _authRepository.GetByDisplayNameAsync(request.DisplayName);

        if (user == null)
        {
            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        // Check lockout
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            return new ApiError("Account is locked due to too many failed login attempts", "ACCOUNT_LOCKED", 423);
        }

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            int newAttempts = user.FailedLoginAttempts + 1;
            DateTime? lockoutEnd = null;

            if (newAttempts >= _authSettings.MaxFailedLoginAttempts)
            {
                lockoutEnd = DateTime.UtcNow.AddMinutes(_authSettings.LockoutMinutes);
            }

            await _authRepository.UpdateFailedLoginAsync(user.Id, newAttempts, lockoutEnd);

            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        // Reset failed attempts on success
        if (user.FailedLoginAttempts > 0)
        {
            await _authRepository.ResetFailedLoginAsync(user.Id);
        }

        await _authRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(string userId)
    {
        var user = await _authRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> GuestLoginAsync(GuestLoginRequest request)
    {
        var existingUser = await _authRepository.GetByDeviceFingerprintAsync(request.DeviceFingerprint);

        if (existingUser != null)
        {
            await _authRepository.UpdateLastLoginAsync(existingUser.Id, DateTime.UtcNow);

            string token = GenerateJwtToken(existingUser);
            return new LoginResponse
            {
                UserId = existingUser.Id,
                DisplayName = existingUser.DisplayName,
                Token = token,
                IsNewUser = false,
            };
        }

        var randomSuffix = RandomNumberGenerator.GetInt32(10000000, 99999999).ToString();
        var user = new UserInfo
        {
            DisplayName = $"Guest_{randomSuffix}",
            PasswordHash = null,
            AuthType = "Guest",
            DeviceFingerprint = request.DeviceFingerprint,
        };

        await _authRepository.CreateUserAsync(user);

        string newToken = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = newToken,
            IsNewUser = true,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> EmailRegisterAsync(EmailRegisterRequest request)
    {
        var (isValid, errorMessage) = PasswordValidator.Validate(request.Password);
        if (!isValid)
        {
            return new ApiError(errorMessage!, "WEAK_PASSWORD", StatusCodes.Status400BadRequest);
        }

        if (await _authRepository.ExistsByEmailAsync(request.Email))
        {
            return new ApiError("Email already exists", "DUPLICATE_EMAIL", StatusCodes.Status409Conflict);
        }

        if (await _authRepository.ExistsByDisplayNameAsync(request.DisplayName))
        {
            return new ApiError("DisplayName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
        }

        var verificationToken = GenerateSecureToken();

        var user = new UserInfo
        {
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AuthType = "Email",
            EmailVerificationToken = verificationToken,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(_authSettings.EmailVerificationExpiryHours),
        };

        await _authRepository.CreateUserAsync(user);

        await _emailService.SendVerificationEmailAsync(request.Email, verificationToken);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
            IsNewUser = true,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> EmailLoginAsync(EmailLoginRequest request)
    {
        var user = await _authRepository.GetByEmailAsync(request.Email);

        if (user == null)
        {
            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        // Check lockout
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            return new ApiError("Account is locked due to too many failed login attempts", "ACCOUNT_LOCKED", 423);
        }

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            int newAttempts = user.FailedLoginAttempts + 1;
            DateTime? lockoutEnd = null;

            if (newAttempts >= _authSettings.MaxFailedLoginAttempts)
            {
                lockoutEnd = DateTime.UtcNow.AddMinutes(_authSettings.LockoutMinutes);
            }

            await _authRepository.UpdateFailedLoginAsync(user.Id, newAttempts, lockoutEnd);

            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        // Reset failed attempts on success
        if (user.FailedLoginAttempts > 0)
        {
            await _authRepository.ResetFailedLoginAsync(user.Id);
        }

        await _authRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<bool, ApiError>> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _authRepository.GetByEmailVerificationTokenAsync(request.Token);

        if (user == null)
        {
            return new ApiError("Invalid verification token", "INVALID_TOKEN", StatusCodes.Status400BadRequest);
        }

        if (user.EmailVerificationExpiry.HasValue && user.EmailVerificationExpiry.Value < DateTime.UtcNow)
        {
            return new ApiError("Verification token has expired", "TOKEN_EXPIRED", StatusCodes.Status400BadRequest);
        }

        await _authRepository.UpdateEmailVerificationAsync(user.Id, true);

        return true;
    }

    public async Task<Result<bool, ApiError>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _authRepository.GetByEmailAsync(request.Email);

        // Always return success to prevent email enumeration
        if (user == null)
        {
            return true;
        }

        var resetToken = GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddMinutes(_authSettings.PasswordResetExpiryMinutes);

        await _authRepository.UpdatePasswordResetTokenAsync(user.Id, resetToken, expiry);
        await _emailService.SendPasswordResetEmailAsync(request.Email, resetToken);

        return true;
    }

    public async Task<Result<bool, ApiError>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _authRepository.GetByPasswordResetTokenAsync(request.Token);

        if (user == null)
        {
            return new ApiError("Invalid reset token", "INVALID_TOKEN", StatusCodes.Status400BadRequest);
        }

        if (user.PasswordResetExpiry.HasValue && user.PasswordResetExpiry.Value < DateTime.UtcNow)
        {
            return new ApiError("Reset token has expired", "TOKEN_EXPIRED", StatusCodes.Status400BadRequest);
        }

        var (isValid, errorMessage) = PasswordValidator.Validate(request.NewPassword);
        if (!isValid)
        {
            return new ApiError(errorMessage!, "WEAK_PASSWORD", StatusCodes.Status400BadRequest);
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _authRepository.UpdatePasswordHashAsync(user.Id, newHash);

        return true;
    }

    public async Task<Result<AccountLinkResponse, ApiError>> LinkEmailAsync(string userId, LinkEmailRequest request)
    {
        var user = await _authRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (user.AuthType != "Guest")
        {
            return new ApiError("Only guest accounts can link to email", "NOT_GUEST", StatusCodes.Status400BadRequest);
        }

        var (isValid, errorMessage) = PasswordValidator.Validate(request.Password);
        if (!isValid)
        {
            return new ApiError(errorMessage!, "WEAK_PASSWORD", StatusCodes.Status400BadRequest);
        }

        if (await _authRepository.ExistsByEmailAsync(request.Email))
        {
            return new ApiError("Email already exists", "DUPLICATE_EMAIL", StatusCodes.Status409Conflict);
        }

        if (await _authRepository.ExistsByDisplayNameAsync(request.DisplayName) &&
            request.DisplayName != user.DisplayName)
        {
            return new ApiError("DisplayName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var verificationToken = GenerateSecureToken();
        var verificationExpiry = DateTime.UtcNow.AddHours(_authSettings.EmailVerificationExpiryHours);

        await _authRepository.LinkEmailAsync(
            userId, request.Email, passwordHash, request.DisplayName,
            verificationToken, verificationExpiry);

        await _emailService.SendVerificationEmailAsync(request.Email, verificationToken);

        // Re-fetch user to get updated state for JWT
        var updatedUser = await _authRepository.GetByIdAsync(userId);
        string token = GenerateJwtToken(updatedUser!);

        return new AccountLinkResponse
        {
            UserId = updatedUser!.Id,
            DisplayName = updatedUser.DisplayName,
            Token = token,
            AuthType = updatedUser.AuthType,
            Email = updatedUser.Email,
        };
    }

    public async Task<Result<AccountLinkResponse, ApiError>> UnlinkEmailAsync(string userId, string deviceFingerprint)
    {
        var user = await _authRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (user.AuthType != "Email")
        {
            return new ApiError("Only email accounts can unlink", "NOT_EMAIL", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(deviceFingerprint) || deviceFingerprint.Length < 16)
        {
            return new ApiError("Invalid device fingerprint", "INVALID_FINGERPRINT", StatusCodes.Status400BadRequest);
        }

        await _authRepository.UnlinkEmailAsync(userId, deviceFingerprint);

        // Re-fetch user to get updated state for JWT
        var updatedUser = await _authRepository.GetByIdAsync(userId);
        string token = GenerateJwtToken(updatedUser!);

        return new AccountLinkResponse
        {
            UserId = updatedUser!.Id,
            DisplayName = updatedUser.DisplayName,
            Token = token,
            AuthType = updatedUser.AuthType,
            Email = null,
        };
    }

    private string GenerateJwtToken(UserInfo user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("level", user.Level.ToString()),
            new Claim("authType", user.AuthType),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
