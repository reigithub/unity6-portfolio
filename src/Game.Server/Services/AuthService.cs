using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Game.Library.Shared.Constants;
using Game.Server.Configuration;
using Game.Server.Database;
using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;
using Game.Server.Validation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using HMACSHA256Crypto = System.Security.Cryptography.HMACSHA256;

namespace Game.Server.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IDbSession _dbSession;
    private readonly JwtSettings _jwtSettings;
    private readonly AuthSettings _authSettings;
    private readonly RequestSigningSettings _signingSettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAuthRepository authRepository,
        IDbSession dbSession,
        IOptions<JwtSettings> jwtSettings,
        IOptions<AuthSettings> authSettings,
        IOptions<RequestSigningSettings> signingSettings,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _authRepository = authRepository;
        _dbSession = dbSession;
        _jwtSettings = jwtSettings.Value;
        _authSettings = authSettings.Value;
        _signingSettings = signingSettings.Value;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request)
    {
        var user = await _authRepository.GetByUserIdStringAsync(request.UserId);

        if (user == null)
        {
            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        // User ID login is only available for guest accounts (transfer password)
        if (user.AuthType != AuthType.Guest)
        {
            return new ApiError("User ID login is only available for guest accounts. Please use email login.",
                "NOT_GUEST", StatusCodes.Status400BadRequest);
        }

        // Check lockout
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            return new ApiError("Account is locked due to too many failed login attempts", "ACCOUNT_LOCKED", 423);
        }

        // Verify against TransferPasswordHash (not PasswordHash)
        if (user.TransferPasswordHash == null ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.TransferPasswordHash))
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

        // Reset failed attempts, update last login, and clear transfer password atomically
        using (var dbTransaction = _dbSession.BeginScope())
        {
            if (user.FailedLoginAttempts > 0)
            {
                await _authRepository.ResetFailedLoginAsync(user.Id);
            }

            await _authRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

            // Clear transfer password after successful login (one-time use)
            await _authRepository.UpdateTransferPasswordHashAsync(user.Id, null);

            dbTransaction.Commit();
        }

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Token = token,
            SigningKey = DeriveUserSigningKey(user.Id),
        };
    }

    public async Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(Guid id)
    {
        var user = await _authRepository.GetByIdAsync(id);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Token = token,
            SigningKey = DeriveUserSigningKey(user.Id),
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
                UserId = existingUser.UserId,
                UserName = existingUser.UserName,
                Token = token,
                IsNewUser = false,
                SigningKey = DeriveUserSigningKey(existingUser.Id),
            };
        }

        var randomSuffix = RandomNumberGenerator.GetInt32(
            _authSettings.GuestNameRandomMin, _authSettings.GuestNameRandomMax).ToString();
        var user = new UserInfo
        {
            UserName = $"Guest_{randomSuffix}",
            PasswordHash = null,
            AuthType = AuthType.Guest,
            DeviceFingerprint = request.DeviceFingerprint,
        };

        var created = await _authRepository.CreateGuestUserAsync(user);
        if (created == null)
        {
            // DeviceFingerprint重複: 既存ユーザーを再取得してログイン
            existingUser = await _authRepository.GetByDeviceFingerprintAsync(request.DeviceFingerprint);
            if (existingUser == null)
            {
                return new ApiError("Guest login failed", "GUEST_LOGIN_FAILED", StatusCodes.Status500InternalServerError);
            }

            await _authRepository.UpdateLastLoginAsync(existingUser.Id, DateTime.UtcNow);
            string existingToken = GenerateJwtToken(existingUser);
            return new LoginResponse
            {
                UserId = existingUser.UserId,
                UserName = existingUser.UserName,
                Token = existingToken,
                IsNewUser = false,
                SigningKey = DeriveUserSigningKey(existingUser.Id),
            };
        }

        string newToken = GenerateJwtToken(created);
        return new LoginResponse
        {
            UserId = created.UserId,
            UserName = created.UserName,
            Token = newToken,
            IsNewUser = true,
            SigningKey = DeriveUserSigningKey(created.Id),
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

        // Reset failed attempts and update last login atomically
        using var tx = _dbSession.BeginScope();

        if (user.FailedLoginAttempts > 0)
        {
            await _authRepository.ResetFailedLoginAsync(user.Id);
        }

        await _authRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        tx.Commit();

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Token = token,
            SigningKey = DeriveUserSigningKey(user.Id),
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

        var emailResult = await _emailService.SendPasswordResetEmailAsync(request.Email, resetToken);
        if (emailResult.IsError)
        {
            _logger.LogWarning("Failed to send password reset email to {Email}", request.Email);
        }

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

    public async Task<Result<AccountLinkResponse, ApiError>> LinkEmailAsync(Guid id, LinkEmailRequest request)
    {
        var user = await _authRepository.GetByIdAsync(id);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (user.AuthType != AuthType.Guest)
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

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var verificationToken = GenerateSecureToken();
        var verificationExpiry = DateTime.UtcNow.AddHours(_authSettings.EmailVerificationExpiryHours);

        try
        {
            await _authRepository.LinkEmailAsync(
                id, request.Email, passwordHash,
                verificationToken, verificationExpiry);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return new ApiError("Email already exists", "DUPLICATE_EMAIL", StatusCodes.Status409Conflict);
        }

        var emailResult = await _emailService.SendVerificationEmailAsync(request.Email, verificationToken);
        if (emailResult.IsError)
        {
            _logger.LogWarning("Failed to send verification email to {Email} for user {UserId}, account was linked but email undelivered",
                request.Email, id);
        }

        // Re-fetch user to get updated state for JWT
        var updatedUser = await _authRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"User {id} not found after update");
        string token = GenerateJwtToken(updatedUser);

        return new AccountLinkResponse
        {
            UserId = updatedUser.UserId,
            UserName = updatedUser.UserName,
            Token = token,
            AuthType = updatedUser.AuthType,
            Email = updatedUser.Email,
            SigningKey = DeriveUserSigningKey(updatedUser.Id),
        };
    }

    public async Task<Result<AccountLinkResponse, ApiError>> UnlinkEmailAsync(Guid id, string deviceFingerprint)
    {
        var user = await _authRepository.GetByIdAsync(id);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (user.AuthType != AuthType.Email)
        {
            return new ApiError("Only email accounts can unlink", "NOT_EMAIL", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(deviceFingerprint) || deviceFingerprint.Length < 16)
        {
            return new ApiError("Invalid device fingerprint", "INVALID_FINGERPRINT", StatusCodes.Status400BadRequest);
        }

        await _authRepository.UnlinkEmailAsync(id, deviceFingerprint);

        // Re-fetch user to get updated state for JWT
        var updatedUser = await _authRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"User {id} not found after update");
        string token = GenerateJwtToken(updatedUser);

        return new AccountLinkResponse
        {
            UserId = updatedUser.UserId,
            UserName = updatedUser.UserName,
            Token = token,
            AuthType = updatedUser.AuthType,
            Email = null,
            SigningKey = DeriveUserSigningKey(updatedUser.Id),
        };
    }

    private string DeriveUserSigningKey(Guid userId)
    {
        var serverSecret = Encoding.UTF8.GetBytes(_signingSettings.SecretKey);
        var userIdBytes = Encoding.UTF8.GetBytes(userId.ToString());
        using var hmac = new HMACSHA256Crypto(serverSecret);
        var derived = hmac.ComputeHash(userIdBytes);
        return Convert.ToBase64String(derived);
    }

    private string GenerateJwtToken(UserInfo user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
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

    public async Task<Result<TransferPasswordResponse, ApiError>> IssueTransferPasswordAsync(Guid id)
    {
        var user = await _authRepository.GetByIdAsync(id);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (user.AuthType != AuthType.Guest)
        {
            return new ApiError("Only guest accounts can issue transfer passwords. Please unlink your email first.",
                "NOT_GUEST", StatusCodes.Status400BadRequest);
        }

        var transferPassword = GenerateTransferPassword();
        var hash = BCrypt.Net.BCrypt.HashPassword(transferPassword);
        await _authRepository.UpdateTransferPasswordHashAsync(id, hash);

        return new TransferPasswordResponse
        {
            TransferPassword = transferPassword,
            UserId = user.UserId
        };
    }

    private static string GenerateTransferPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // I,O,0,1 excluded
        var random = RandomNumberGenerator.GetBytes(12);
        var result = new char[12];
        for (int i = 0; i < 12; i++)
            result[i] = chars[random[i] % chars.Length];
        return new string(result);
    }
}
