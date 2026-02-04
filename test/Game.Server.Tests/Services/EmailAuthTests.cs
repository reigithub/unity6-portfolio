using System.Security.Cryptography;
using Dapper;
using Game.Server.Configuration;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Dapper;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tables;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace Game.Server.Tests.Services;

[Collection("Database")]
public class EmailAuthTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;
    private Mock<IEmailService> _mockEmailService = null!;

    public EmailAuthTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await _postgres.ResetUserDataAsync();
        _connectionFactory = TestDataFixture.CreateConnectionFactory(_postgres.ConnectionString);
        _mockEmailService = new Mock<IEmailService>();
        _mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- EmailLogin ---

    [Fact]
    public async Task EmailLoginAsync_ValidCredentials_ReturnsToken()
    {
        await SeedEmailUserAsync("login@example.com", "Password1!", "LoginUser");
        var service = CreateAuthService();

        var result = await service.EmailLoginAsync(new EmailLoginRequest
        {
            Email = "login@example.com",
            Password = "Password1!",
        });

        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("LoginUser", response.UserName);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task EmailLoginAsync_WrongPassword_ReturnsUnauthorized()
    {
        await SeedEmailUserAsync("wrongpw@example.com", "Password1!", "WrongPwUser");
        var service = CreateAuthService();

        var result = await service.EmailLoginAsync(new EmailLoginRequest
        {
            Email = "wrongpw@example.com",
            Password = "WrongPassword1!",
        });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
    }

    [Fact]
    public async Task EmailLoginAsync_NonExistentEmail_ReturnsUnauthorized()
    {
        var service = CreateAuthService();

        var result = await service.EmailLoginAsync(new EmailLoginRequest
        {
            Email = "noexist@example.com",
            Password = "Password1!",
        });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
    }

    [Fact]
    public async Task EmailLoginAsync_LockedAccount_ReturnsLocked()
    {
        await SeedEmailUserAsync("locked@example.com", "Password1!", "LockedUser");
        var service = CreateAuthService();

        for (int i = 0; i < 5; i++)
        {
            await service.EmailLoginAsync(new EmailLoginRequest
            {
                Email = "locked@example.com",
                Password = "WrongPassword1!",
            });
        }

        var result = await service.EmailLoginAsync(new EmailLoginRequest
        {
            Email = "locked@example.com",
            Password = "Password1!",
        });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("ACCOUNT_LOCKED", error.ErrorCode);
        Assert.Equal(423, error.StatusCode);
    }

    // --- VerifyEmail ---

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_VerifiesEmail()
    {
        await SeedEmailUserAsync("verify@example.com", "Password1!", "VerifyUser");

        // Get the token from the database
        var token = await GetEmailVerificationTokenAsync("verify@example.com");
        Assert.NotNull(token);

        var service = CreateAuthService();
        var result = await service.VerifyEmailAsync(new VerifyEmailRequest { Token = token });

        var success = AuthServiceTests.ExtractSuccess(result);
        Assert.True(success);
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsError()
    {
        await SeedEmailUserAsync("expired@example.com", "Password1!", "ExpiredUser");

        // Manually set expiry to the past
        var token = await GetEmailVerificationTokenAsync("expired@example.com");
        await SetEmailVerificationExpiryAsync("expired@example.com", DateTime.UtcNow.AddHours(-1));

        var service = CreateAuthService();
        var result = await service.VerifyEmailAsync(new VerifyEmailRequest { Token = token! });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("TOKEN_EXPIRED", error.ErrorCode);
    }

    [Fact]
    public async Task VerifyEmailAsync_InvalidToken_ReturnsError()
    {
        var service = CreateAuthService();

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest { Token = "invalid-token" });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_TOKEN", error.ErrorCode);
    }

    // --- ForgotPassword ---

    [Fact]
    public async Task ForgotPasswordAsync_ExistingEmail_SendsResetEmail()
    {
        await SeedEmailUserAsync("forgot@example.com", "Password1!", "ForgotUser");
        var service = CreateAuthService();

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "forgot@example.com" });

        var success = AuthServiceTests.ExtractSuccess(result);
        Assert.True(success);
        _mockEmailService.Verify(
            e => e.SendPasswordResetEmailAsync("forgot@example.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_NonExistentEmail_ReturnsSuccessWithoutSending()
    {
        var service = CreateAuthService();

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "nobody@example.com" });

        var success = AuthServiceTests.ExtractSuccess(result);
        Assert.True(success);
        _mockEmailService.Verify(
            e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // --- ResetPassword ---

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ResetsPassword()
    {
        await SeedEmailUserAsync("reset@example.com", "Password1!", "ResetUser");
        var service = CreateAuthService();

        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "reset@example.com" });

        var resetToken = await GetPasswordResetTokenAsync("reset@example.com");
        Assert.NotNull(resetToken);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = resetToken,
            NewPassword = "NewPassword1!",
        });

        var success = AuthServiceTests.ExtractSuccess(result);
        Assert.True(success);

        // Verify new password works
        var loginResult = await service.EmailLoginAsync(new EmailLoginRequest
        {
            Email = "reset@example.com",
            Password = "NewPassword1!",
        });
        var loginResponse = AuthServiceTests.ExtractSuccess(loginResult);
        Assert.NotNull(loginResponse);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsError()
    {
        await SeedEmailUserAsync("resetexp@example.com", "Password1!", "ResetExpUser");
        var service = CreateAuthService();

        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "resetexp@example.com" });

        var resetToken = await GetPasswordResetTokenAsync("resetexp@example.com");
        await SetPasswordResetExpiryAsync("resetexp@example.com", DateTime.UtcNow.AddHours(-1));

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = resetToken!,
            NewPassword = "NewPassword1!",
        });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("TOKEN_EXPIRED", error.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakNewPassword_ReturnsError()
    {
        await SeedEmailUserAsync("resetweak@example.com", "Password1!", "ResetWeakUser");
        var service = CreateAuthService();

        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "resetweak@example.com" });

        var resetToken = await GetPasswordResetTokenAsync("resetweak@example.com");

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = resetToken!,
            NewPassword = "weak",
        });

        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("WEAK_PASSWORD", error.ErrorCode);
    }

    private AuthService CreateAuthService()
    {
        var authRepo = new DapperAuthRepository(_connectionFactory);

        return new AuthService(
            authRepo,
            TestDataFixture.GetJwtOptions(),
            TestDataFixture.GetAuthOptions(),
            _mockEmailService.Object);
    }

    private async Task<string?> GetEmailVerificationTokenAsync(string email)
    {
        using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<string?>(
            @"SELECT ""EmailVerificationToken"" FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email",
            new { Email = email });
    }

    private async Task SetEmailVerificationExpiryAsync(string email, DateTime expiry)
    {
        using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo"" SET ""EmailVerificationExpiry"" = @Expiry WHERE ""Email"" = @Email",
            new { Email = email, Expiry = expiry });
    }

    private async Task<string?> GetPasswordResetTokenAsync(string email)
    {
        using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<string?>(
            @"SELECT ""PasswordResetToken"" FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email",
            new { Email = email });
    }

    private async Task SetPasswordResetExpiryAsync(string email, DateTime expiry)
    {
        using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo"" SET ""PasswordResetExpiry"" = @Expiry WHERE ""Email"" = @Email",
            new { Email = email, Expiry = expiry });
    }

    private async Task SeedEmailUserAsync(string email, string password, string userName)
    {
        var verificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var user = new UserInfo
        {
            UserName = userName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            AuthType = "Email",
            EmailVerificationToken = verificationToken,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24),
        };

        using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @UserName, @PasswordHash, @Level, @RegisteredAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            user);
    }
}
