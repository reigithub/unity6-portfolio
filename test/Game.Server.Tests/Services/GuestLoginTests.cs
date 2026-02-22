using Game.Library.Shared.Dto;
using Game.Server.Repositories;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Services;

[Collection("Database")]
public class GuestLoginTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private Game.Server.Database.IDbConnectionFactory _connectionFactory = null!;
    private Game.Server.Database.IDbSession _dbSession = null!;

    public GuestLoginTests(PostgresContainerFixture postgres)
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
        Assert.StartsWith("Guest_", response.UserName);
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

    [Fact]
    public async Task GuestLoginAsync_ConcurrentSameDevice_OnlyCreatesOneUser()
    {
        // Arrange
        var fingerprint = "concurrent-test-device-001";
        var request = new GuestLoginRequest { DeviceFingerprint = fingerprint };

        // Act: 同時に10リクエスト（各タスクが独自のDB接続を使用）
        var tasks = Enumerable.Range(0, 10)
            .Select(async _ =>
            {
                var session = TestDataFixture.CreateDbSession(_connectionFactory);
                try
                {
                    var service = CreateAuthService(session);
                    return await service.GuestLoginAsync(request);
                }
                finally
                {
                    await session.DisposeAsync();
                }
            })
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert: 全リクエスト成功し、全て同じUserId
        var userIds = results
            .Select(r => AuthServiceTests.ExtractSuccess(r)!.UserId)
            .Distinct()
            .ToList();
        Assert.Single(userIds);

        // 新規ユーザーは1人だけ
        var newUserCount = results.Count(r => AuthServiceTests.ExtractSuccess(r)!.IsNewUser);
        Assert.Equal(1, newUserCount);
    }

    private AuthService CreateAuthService()
    {
        return CreateAuthService(_dbSession);
    }

    private static AuthService CreateAuthService(Game.Server.Database.IDbSession dbSession)
    {
        var authRepo = new AuthRepository(dbSession);
        var mockEmailService = new Mock<IEmailService>();
        mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        return new AuthService(
            authRepo,
            dbSession,
            TestDataFixture.GetJwtOptions(),
            TestDataFixture.GetAuthOptions(),
            TestDataFixture.GetSigningOptions(),
            mockEmailService.Object,
            new Mock<ILogger<AuthService>>().Object);
    }
}
