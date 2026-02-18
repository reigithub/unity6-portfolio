using Game.Server.Configuration;
using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Repositories;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Game.Server.Tests.Services;

/// <summary>
/// アカウントロックアウト機能のテスト
/// EmailLoginAsync を使用（LoginAsyncはGuestアカウント専用のため）
/// </summary>
[Collection("Database")]
public class AccountLockoutTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;
    private Game.Server.Database.IDbSession _dbSession = null!;

    // テスト用のEmailユーザー情報
    private const string TestEmail = "existing@example.com";
    private const string TestPassword = "Password1!";
    private const string TestUserName = "EmailPlayer";

    public AccountLockoutTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await _postgres.ResetUserDataAsync();
        _connectionFactory = TestDataFixture.CreateConnectionFactory(_postgres.ConnectionString);
        _dbSession = TestDataFixture.CreateDbSession(_connectionFactory);
    }

    public async Task DisposeAsync()
    {
        await _dbSession.DisposeAsync();
    }

    [Fact]
    public async Task EmailLoginAsync_FiveFailedAttempts_LocksAccount()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new EmailLoginRequest { Email = TestEmail, Password = "WrongPass1!" };

        // Act - fail 5 times
        for (int i = 0; i < 5; i++)
        {
            await service.EmailLoginAsync(wrongRequest);
        }

        // Try with correct password - should be locked
        var correctRequest = new EmailLoginRequest { Email = TestEmail, Password = TestPassword };
        var result = await service.EmailLoginAsync(correctRequest);

        // Assert
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("ACCOUNT_LOCKED", error.ErrorCode);
        Assert.Equal(423, error.StatusCode);
    }

    [Fact]
    public async Task EmailLoginAsync_SuccessResetsFailedAttempts()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new EmailLoginRequest { Email = TestEmail, Password = "WrongPass1!" };

        // Act - fail 3 times
        for (int i = 0; i < 3; i++)
        {
            await service.EmailLoginAsync(wrongRequest);
        }

        // Login with correct password
        var correctRequest = new EmailLoginRequest { Email = TestEmail, Password = TestPassword };
        var successResult = await service.EmailLoginAsync(correctRequest);

        // Assert - should succeed
        var response = AuthServiceTests.ExtractSuccess(successResult);
        Assert.NotNull(response);
        Assert.Equal(TestUserName, response.UserName);

        // After success, fail 4 more times - should still not be locked (counter was reset)
        for (int i = 0; i < 4; i++)
        {
            await service.EmailLoginAsync(wrongRequest);
        }

        var afterResetResult = await service.EmailLoginAsync(correctRequest);
        var afterResetResponse = AuthServiceTests.ExtractSuccess(afterResetResult);
        Assert.NotNull(afterResetResponse);
    }

    [Fact]
    public async Task EmailLoginAsync_LockoutExpired_AllowsLogin()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);

        // Use a very short lockout period for testing
        var authSettings = new AuthSettings
        {
            MaxFailedLoginAttempts = 5,
            LockoutMinutes = 0, // 0 minutes = immediate expiry
        };
        var service = CreateAuthService(Options.Create(authSettings));
        var wrongRequest = new EmailLoginRequest { Email = TestEmail, Password = "WrongPass1!" };

        // Act - fail 5 times to trigger lockout
        for (int i = 0; i < 5; i++)
        {
            await service.EmailLoginAsync(wrongRequest);
        }

        // With 0-minute lockout, the lock has already expired
        var correctRequest = new EmailLoginRequest { Email = TestEmail, Password = TestPassword };
        var result = await service.EmailLoginAsync(correctRequest);

        // Assert - should succeed because lockout expired
        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal(TestUserName, response.UserName);
    }

    #region Guest Account Lockout Tests (LoginAsync with TransferPassword)

    // テスト用のGuestユーザー情報（TransferPassword付き）
    private const string GuestUserId = "000000000006";
    private const string GuestTransferPassword = "TransferPW1!";
    private const string GuestUserName = "Guest_TransferPW";

    [Fact]
    public async Task LoginAsync_GuestAccount_FiveFailedAttempts_LocksAccount()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new LoginRequest { UserId = GuestUserId, Password = "WrongPass1!" };

        // Act - fail 5 times
        for (int i = 0; i < 5; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        // Try with correct password - should be locked
        var correctRequest = new LoginRequest { UserId = GuestUserId, Password = GuestTransferPassword };
        var result = await service.LoginAsync(correctRequest);

        // Assert
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("ACCOUNT_LOCKED", error.ErrorCode);
        Assert.Equal(423, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_GuestAccount_SuccessAfterFailedAttempts()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new LoginRequest { UserId = GuestUserId, Password = "WrongPass1!" };

        // Act - fail 3 times (not enough to lock)
        for (int i = 0; i < 3; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        // Login with correct password - should succeed (not locked yet)
        var correctRequest = new LoginRequest { UserId = GuestUserId, Password = GuestTransferPassword };
        var successResult = await service.LoginAsync(correctRequest);

        // Assert - should succeed
        var response = AuthServiceTests.ExtractSuccess(successResult);
        Assert.NotNull(response);
        Assert.Equal(GuestUserName, response.UserName);

        // Note: After successful login, TransferPasswordHash is cleared (one-time use)
        // Further login attempts would fail with INVALID_CREDENTIALS, which is expected behavior
    }

    [Fact]
    public async Task LoginAsync_NonGuestAccount_ReturnsNotGuestError()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Try to use LoginAsync with Email account (not Guest)
        var request = new LoginRequest { UserId = "000000000005", Password = TestPassword };

        // Act
        var result = await service.LoginAsync(request);

        // Assert - should return NOT_GUEST error
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("NOT_GUEST", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    #endregion

    private AuthService CreateAuthService(IOptions<AuthSettings>? authOptions = null)
    {
        var authRepo = new AuthRepository(_dbSession);
        var mockEmailService = new Mock<IEmailService>();
        mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        return new AuthService(
            authRepo,
            _dbSession,
            TestDataFixture.GetJwtOptions(),
            authOptions ?? TestDataFixture.GetAuthOptions(),
            TestDataFixture.GetSigningOptions(),
            mockEmailService.Object,
            new Mock<ILogger<AuthService>>().Object);
    }
}
