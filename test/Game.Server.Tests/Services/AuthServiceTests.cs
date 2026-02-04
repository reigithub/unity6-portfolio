using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Dapper;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Game.Server.Tests.Services;

[Collection("Database")]
public class AuthServiceTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;

    public AuthServiceTests(PostgresContainerFixture postgres)
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
    public async Task RegisterAsync_ValidRequest_ReturnsLoginResponse()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new RegisterRequest { UserName = "NewUser", Password = "Password123!" };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        LoginResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("NewUser", response.UserName);
        Assert.NotEmpty(response.Token);
        Assert.NotEmpty(response.UserId);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateName_ReturnsConflictError()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new RegisterRequest { UserName = "Player1", Password = "Password123!" };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_NAME", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LoginRequest { UserName = "Player1", Password = "Password1!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        LoginResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Player1", response.UserName);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LoginRequest { UserName = "Player1", Password = "WrongPassword" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { UserName = "NoSuchUser", Password = "Password123!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
    }

    private AuthService CreateAuthService()
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
            TestDataFixture.GetAuthOptions(),
            mockEmailService.Object);
    }

    [Fact]
    public async Task LinkEmailAsync_GuestUser_ReturnsAccountLinkResponse()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LinkEmailRequest
        {
            Email = "newlink@example.com",
            Password = "LinkPassword123!",
            UserName = "LinkedUser"
        };

        // Act
        var result = await service.LinkEmailAsync(TestDataFixture.GuestUserId, request);

        // Assert
        AccountLinkResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Email", response.AuthType);
        Assert.Equal("LinkedUser", response.UserName);
        Assert.Equal("newlink@example.com", response.Email);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task LinkEmailAsync_NonGuestUser_ReturnsBadRequest()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LinkEmailRequest
        {
            Email = "link@example.com",
            Password = "LinkPassword123!",
            UserName = "LinkUser"
        };

        // Act
        var result = await service.LinkEmailAsync(TestDataFixture.User1Id, request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("NOT_GUEST", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task LinkEmailAsync_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LinkEmailRequest
        {
            Email = "existing@example.com",
            Password = "LinkPassword123!",
            UserName = "LinkUser"
        };

        // Act
        var result = await service.LinkEmailAsync(TestDataFixture.GuestUserId, request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_EMAIL", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task UnlinkEmailAsync_EmailUser_ReturnsGuestResponse()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Act
        var result = await service.UnlinkEmailAsync(TestDataFixture.EmailUserId, "device-fingerprint-0123456789abcdef");

        // Assert
        AccountLinkResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Guest", response.AuthType);
        Assert.Null(response.Email);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task UnlinkEmailAsync_GuestUser_ReturnsBadRequest()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Act
        var result = await service.UnlinkEmailAsync(TestDataFixture.GuestUserId, "device-fingerprint-0123456789abcdef");

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("NOT_EMAIL", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    internal static TSuccess? ExtractSuccess<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TSuccess? success = default;
        result.Match(
            s => { success = s; return new OkResult(); },
            e => new OkResult());
        return success;
    }

    internal static TError? ExtractError<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TError? error = default;
        result.Match(
            s => new OkResult(),
            e => { error = e; return new OkResult(); });
        return error;
    }
}
