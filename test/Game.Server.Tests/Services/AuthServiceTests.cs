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
    public async Task LoginAsync_GuestWithTransferPassword_ReturnsToken()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        // User ID login with transfer password (Guest account)
        var request = new LoginRequest { UserId = "000000000006", Password = "TransferPW1!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        LoginResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Guest_TransferPW", response.UserName);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task LoginAsync_GuestWithInvalidTransferPassword_ReturnsUnauthorized()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        var request = new LoginRequest { UserId = "000000000006", Password = "WrongPassword" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_NonGuestUser_ReturnsBadRequest()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();
        // Attempting User ID login with non-Guest (Password type) user should fail
        var request = new LoginRequest { UserId = "000000000001", Password = "Password1!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("NOT_GUEST", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { UserId = "999999999999", Password = "Password123!" };

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
            Password = "LinkPassword123!"
        };

        // Act
        var result = await service.LinkEmailAsync(TestDataFixture.GuestUserId, request);

        // Assert
        AccountLinkResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Email", response.AuthType);
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
            Password = "LinkPassword123!"
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
            Password = "LinkPassword123!"
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

    [Fact]
    public async Task IssueTransferPasswordAsync_GuestUser_ReturnsTransferPassword()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Act
        var result = await service.IssueTransferPasswordAsync(TestDataFixture.GuestUserId);

        // Assert
        TransferPasswordResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("000000000004", response.UserId);
        Assert.NotEmpty(response.TransferPassword);
        Assert.Equal(12, response.TransferPassword.Length);
    }

    [Fact]
    public async Task IssueTransferPasswordAsync_NonGuestUser_ReturnsBadRequest()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Act
        var result = await service.IssueTransferPasswordAsync(TestDataFixture.EmailUserId);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("NOT_GUEST", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task IssueTransferPasswordAsync_ThenLogin_Succeeds()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Act: Issue transfer password
        var issueResult = await service.IssueTransferPasswordAsync(TestDataFixture.GuestUserId);
        var transferPassword = ExtractSuccess(issueResult);
        Assert.NotNull(transferPassword);

        // Act: Login with the issued transfer password
        var loginRequest = new LoginRequest
        {
            UserId = transferPassword.UserId,
            Password = transferPassword.TransferPassword
        };
        var loginResult = await service.LoginAsync(loginRequest);

        // Assert
        LoginResponse? loginResponse = ExtractSuccess(loginResult);
        Assert.NotNull(loginResponse);
        Assert.Equal("Guest_12345678", loginResponse.UserName);
        Assert.NotEmpty(loginResponse.Token);
    }

    [Fact]
    public async Task TransferPassword_OneTimeUse_SecondLoginFails()
    {
        // Arrange
        await TestDataFixture.SeedTestDataAsync(_postgres.ConnectionString);
        var service = CreateAuthService();

        // Issue transfer password
        var issueResult = await service.IssueTransferPasswordAsync(TestDataFixture.GuestUserId);
        var transferPassword = ExtractSuccess(issueResult);
        Assert.NotNull(transferPassword);

        var loginRequest = new LoginRequest
        {
            UserId = transferPassword.UserId,
            Password = transferPassword.TransferPassword
        };

        // First login should succeed
        var firstLoginResult = await service.LoginAsync(loginRequest);
        Assert.NotNull(ExtractSuccess(firstLoginResult));

        // Second login with the same password should fail (one-time use)
        var secondLoginResult = await service.LoginAsync(loginRequest);
        ApiError? error = ExtractError(secondLoginResult);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
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
