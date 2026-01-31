using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Dapper;
using Game.Server.Services;
using Game.Server.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Game.Server.Data.IDbConnectionFactory _connectionFactory;

    public AuthServiceTests()
    {
        _connection = TestDataFixture.CreateSqliteConnection();
        _connectionFactory = TestDataFixture.CreateConnectionFactory(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsLoginResponse()
    {
        // Arrange
        var authRepo = new DapperAuthRepository(_connectionFactory);
        var service = new AuthService(authRepo, TestDataFixture.GetJwtOptions());
        var request = new RegisterRequest { DisplayName = "NewUser", Password = "Password123!" };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        LoginResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("NewUser", response.DisplayName);
        Assert.NotEmpty(response.Token);
        Assert.NotEmpty(response.UserId);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateName_ReturnsConflictError()
    {
        // Arrange
        var seededConnection = await TestDataFixture.CreateSeededConnectionAsync();
        var factory = TestDataFixture.CreateConnectionFactory(seededConnection);
        var authRepo = new DapperAuthRepository(factory);
        var service = new AuthService(authRepo, TestDataFixture.GetJwtOptions());
        var request = new RegisterRequest { DisplayName = "Player1", Password = "Password123!" };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_NAME", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);

        seededConnection.Dispose();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var seededConnection = await TestDataFixture.CreateSeededConnectionAsync();
        var factory = TestDataFixture.CreateConnectionFactory(seededConnection);
        var authRepo = new DapperAuthRepository(factory);
        var service = new AuthService(authRepo, TestDataFixture.GetJwtOptions());
        var request = new LoginRequest { DisplayName = "Player1", Password = "Password1!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        LoginResponse? response = ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.Equal("Player1", response.DisplayName);
        Assert.NotEmpty(response.Token);

        seededConnection.Dispose();
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var seededConnection = await TestDataFixture.CreateSeededConnectionAsync();
        var factory = TestDataFixture.CreateConnectionFactory(seededConnection);
        var authRepo = new DapperAuthRepository(factory);
        var service = new AuthService(authRepo, TestDataFixture.GetJwtOptions());
        var request = new LoginRequest { DisplayName = "Player1", Password = "WrongPassword" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
        Assert.Equal(401, error.StatusCode);

        seededConnection.Dispose();
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var authRepo = new DapperAuthRepository(_connectionFactory);
        var service = new AuthService(authRepo, TestDataFixture.GetJwtOptions());
        var request = new LoginRequest { DisplayName = "NoSuchUser", Password = "Password123!" };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        ApiError? error = ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error.ErrorCode);
    }

    private static TSuccess? ExtractSuccess<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TSuccess? success = default;
        result.Match(
            s => { success = s; return new OkResult(); },
            e => new OkResult());
        return success;
    }

    private static TError? ExtractError<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TError? error = default;
        result.Match(
            s => new OkResult(),
            e => { error = e; return new OkResult(); });
        return error;
    }
}
