using Game.Server.Configuration;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Dapper;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Moq;

namespace Game.Server.Tests.Services;

[Collection("Database")]
public class AccountLockoutTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;

    public AccountLockoutTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await _postgres.ResetUserDataAsync();
        _connectionFactory = TestDataFixture.CreateConnectionFactory(_postgres.ConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LoginAsync_FiveFailedAttempts_LocksAccount()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new LoginRequest { UserId = "000000000001", Password = "WrongPass1!" };

        // Act - fail 5 times
        for (int i = 0; i < 5; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        // Try with correct password - should be locked
        var correctRequest = new LoginRequest { UserId = "000000000001", Password = "Password1!" };
        var result = await service.LoginAsync(correctRequest);

        // Assert
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("ACCOUNT_LOCKED", error.ErrorCode);
        Assert.Equal(423, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_SuccessResetsFailedAttempts()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var wrongRequest = new LoginRequest { UserId = "000000000001", Password = "WrongPass1!" };

        // Act - fail 3 times
        for (int i = 0; i < 3; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        // Login with correct password
        var correctRequest = new LoginRequest { UserId = "000000000001", Password = "Password1!" };
        var successResult = await service.LoginAsync(correctRequest);

        // Assert - should succeed
        var response = AuthServiceTests.ExtractSuccess(successResult);
        Assert.NotNull(response);
        Assert.Equal("Player1", response.UserName);

        // After success, fail 4 more times - should still not be locked (counter was reset)
        for (int i = 0; i < 4; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        var afterResetResult = await service.LoginAsync(correctRequest);
        var afterResetResponse = AuthServiceTests.ExtractSuccess(afterResetResult);
        Assert.NotNull(afterResetResponse);
    }

    [Fact]
    public async Task LoginAsync_LockoutExpired_AllowsLogin()
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
        var wrongRequest = new LoginRequest { UserId = "000000000001", Password = "WrongPass1!" };

        // Act - fail 5 times to trigger lockout
        for (int i = 0; i < 5; i++)
        {
            await service.LoginAsync(wrongRequest);
        }

        // With 0-minute lockout, the lock has already expired
        var correctRequest = new LoginRequest { UserId = "000000000001", Password = "Password1!" };
        var result = await service.LoginAsync(correctRequest);

        // Assert - should succeed because lockout expired
        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Player1", response.UserName);
    }

    private AuthService CreateAuthService(IOptions<AuthSettings>? authOptions = null)
    {
        var authRepo = new DapperAuthRepository(_connectionFactory);
        var mockEmailService = new Mock<IEmailService>();
        mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        return new AuthService(
            authRepo,
            TestDataFixture.GetJwtOptions(),
            authOptions ?? TestDataFixture.GetAuthOptions(),
            mockEmailService.Object);
    }
}
