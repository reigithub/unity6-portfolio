using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Dapper;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Moq;

namespace Game.Server.Tests.Services;

[Collection("Database")]
public class GuestLoginTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;

    public GuestLoginTests(PostgresContainerFixture postgres)
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
    public async Task GuestLoginAsync_NewDevice_CreatesNewUser()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new GuestLoginRequest { DeviceFingerprint = "test-device-12345678" };

        // Act
        var result = await service.GuestLoginAsync(request);

        // Assert
        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.True(response.IsNewUser);
        Assert.NotEmpty(response.Token);
        Assert.NotEmpty(response.UserId);
        Assert.StartsWith("Guest_", response.DisplayName);
    }

    [Fact]
    public async Task GuestLoginAsync_SameDevice_ReturnsSameUser()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new GuestLoginRequest { DeviceFingerprint = "test-device-same-1234" };

        // Act
        var firstResult = await service.GuestLoginAsync(request);
        var secondResult = await service.GuestLoginAsync(request);

        // Assert
        var firstResponse = AuthServiceTests.ExtractSuccess(firstResult);
        var secondResponse = AuthServiceTests.ExtractSuccess(secondResult);
        Assert.NotNull(firstResponse);
        Assert.NotNull(secondResponse);
        Assert.True(firstResponse.IsNewUser);
        Assert.False(secondResponse.IsNewUser);
        Assert.Equal(firstResponse.UserId, secondResponse.UserId);
    }

    [Fact]
    public async Task GuestLoginAsync_DifferentDevices_CreatesDifferentUsers()
    {
        // Arrange
        var service = CreateAuthService();
        var request1 = new GuestLoginRequest { DeviceFingerprint = "test-device-aaaaaaaa" };
        var request2 = new GuestLoginRequest { DeviceFingerprint = "test-device-bbbbbbbb" };

        // Act
        var result1 = await service.GuestLoginAsync(request1);
        var result2 = await service.GuestLoginAsync(request2);

        // Assert
        var response1 = AuthServiceTests.ExtractSuccess(result1);
        var response2 = AuthServiceTests.ExtractSuccess(result2);
        Assert.NotNull(response1);
        Assert.NotNull(response2);
        Assert.NotEqual(response1.UserId, response2.UserId);
        Assert.True(response1.IsNewUser);
        Assert.True(response2.IsNewUser);
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
}
