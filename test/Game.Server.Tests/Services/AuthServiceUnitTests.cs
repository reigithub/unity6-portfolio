using Game.Library.Shared.Constants;
using Game.Library.Shared.Dto;
using Game.Server.Database;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tables;
using Game.Server.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;

namespace Game.Server.Tests.Services;

/// <summary>
/// AuthService のユニットテスト（Moq ベース）
/// レースコンディション修正の PostgresException ハンドリングを検証
/// </summary>
public class AuthServiceUnitTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepo;
    private readonly Mock<IDbSession> _mockDbSession;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly AuthService _service;

    public AuthServiceUnitTests()
    {
        _mockAuthRepo = new Mock<IAuthRepository>();
        _mockDbSession = new Mock<IDbSession>();
        _mockEmailService = new Mock<IEmailService>();
        _mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _service = new AuthService(
            _mockAuthRepo.Object,
            _mockDbSession.Object,
            TestDataFixture.GetJwtOptions(),
            TestDataFixture.GetAuthOptions(),
            TestDataFixture.GetSigningOptions(),
            _mockEmailService.Object,
            new Mock<ILogger<AuthService>>().Object);
    }

    /// <summary>
    /// Finding #4: LinkEmailAsync — ExistsByEmailAsync(CHECK) は false を返すが、
    /// 直後の LinkEmailAsync(ACT) で PostgresException 23505 が発生するレースコンディション。
    /// catch ブロックが DUPLICATE_EMAIL (409) を正しく返すことを検証。
    /// </summary>
    [Fact]
    public async Task LinkEmailAsync_Returns409_WhenUniqueViolationOnRace()
    {
        // Arrange
        var userId = "test-user-001";
        var guestUser = new UserInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "Guest_123",
            AuthType = AuthType.Guest,
            Level = 1,
            RegisteredAt = DateTime.UtcNow,
        };

        _mockAuthRepo.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(guestUser);
        _mockAuthRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(false); // CHECK passes — email appears available
        _mockAuthRepo.Setup(r => r.LinkEmailAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(CreateUniqueViolationException()); // ACT fails — race condition

        var request = new LinkEmailRequest
        {
            Email = "taken@example.com",
            Password = "StrongP@ssw0rd123!",
        };

        // Act
        var result = await _service.LinkEmailAsync(userId, request);

        // Assert
        Assert.True(result.IsError);
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_EMAIL", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
    }

    /// <summary>
    /// LinkEmailAsync — PostgresException が 23505 以外の SqlState の場合は
    /// catch されずに例外が伝播することを検証。
    /// </summary>
    [Fact]
    public async Task LinkEmailAsync_ThrowsException_WhenNonUniqueViolationPostgresError()
    {
        // Arrange
        var userId = "test-user-002";
        var guestUser = new UserInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "Guest_456",
            AuthType = AuthType.Guest,
            Level = 1,
            RegisteredAt = DateTime.UtcNow,
        };

        _mockAuthRepo.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(guestUser);
        _mockAuthRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.LinkEmailAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(CreatePostgresException("23503")); // foreign key violation, not unique

        var request = new LinkEmailRequest
        {
            Email = "test@example.com",
            Password = "StrongP@ssw0rd123!",
        };

        // Act & Assert — non-23505 should propagate
        await Assert.ThrowsAsync<PostgresException>(() => _service.LinkEmailAsync(userId, request));
    }

    /// <summary>
    /// Finding #2 の補足: GuestLoginAsync — CreateGuestUserAsync が null を返した場合
    /// （ON CONFLICT DO NOTHING でINSERTスキップ）、既存ユーザーを再取得してログインする。
    /// </summary>
    [Fact]
    public async Task GuestLoginAsync_ReturnsExistingUser_WhenInsertConflicts()
    {
        // Arrange: CreateGuestUserAsync returns null (ON CONFLICT DO NOTHING)
        var existingUser = new UserInfo
        {
            Id = Guid.NewGuid(),
            UserId = "existing-user-001",
            UserName = "Guest_999",
            AuthType = AuthType.Guest,
            Level = 1,
            RegisteredAt = DateTime.UtcNow,
            DeviceFingerprint = "conflict-device-fp",
        };

        // First GetByDeviceFingerprintAsync returns null (initial check)
        // After CreateGuestUserAsync returns null, second call returns the existing user
        var callCount = 0;
        _mockAuthRepo.Setup(r => r.GetByDeviceFingerprintAsync("conflict-device-fp"))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? null : existingUser;
            });
        _mockAuthRepo.Setup(r => r.CreateGuestUserAsync(It.IsAny<UserInfo>()))
            .ReturnsAsync((UserInfo?)null); // conflict occurred
        _mockAuthRepo.Setup(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var request = new GuestLoginRequest { DeviceFingerprint = "conflict-device-fp" };

        // Act
        var result = await _service.GuestLoginAsync(request);

        // Assert
        var response = AuthServiceTests.ExtractSuccess(result);
        Assert.NotNull(response);
        Assert.False(response.IsNewUser);
        Assert.Equal("existing-user-001", response.UserId);
    }

    /// <summary>
    /// GuestLoginAsync — CreateGuestUserAsync が null を返し、再取得も null の場合は
    /// 500 エラーを返すことを検証。
    /// </summary>
    [Fact]
    public async Task GuestLoginAsync_Returns500_WhenConflictAndRefetchFails()
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.GetByDeviceFingerprintAsync(It.IsAny<string>()))
            .ReturnsAsync((UserInfo?)null);
        _mockAuthRepo.Setup(r => r.CreateGuestUserAsync(It.IsAny<UserInfo>()))
            .ReturnsAsync((UserInfo?)null);

        var request = new GuestLoginRequest { DeviceFingerprint = "broken-device-fp" };

        // Act
        var result = await _service.GuestLoginAsync(request);

        // Assert
        Assert.True(result.IsError);
        var error = AuthServiceTests.ExtractError(result);
        Assert.NotNull(error);
        Assert.Equal("GUEST_LOGIN_FAILED", error.ErrorCode);
        Assert.Equal(500, error.StatusCode);
    }

    /// <summary>
    /// PostgresException を生成するヘルパー（Npgsql 8.x+ の公開コンストラクタを使用）。
    /// フォールバックとしてリフレクションで内部コンストラクタを試行する。
    /// </summary>
    private static PostgresException CreateUniqueViolationException()
    {
        return CreatePostgresException("23505");
    }

    private static PostgresException CreatePostgresException(string sqlState)
    {
        var type = typeof(PostgresException);

        // Npgsql 8.x+: public PostgresException(string messageText, string severity, string invariantSeverity, string sqlState)
        var ctor4 = type.GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(string) });
        if (ctor4 != null)
        {
            return (PostgresException)ctor4.Invoke(new object[]
            {
                "duplicate key value violates unique constraint",
                "ERROR",
                "ERROR",
                sqlState,
            });
        }

        // Fallback: try internal constructors via reflection
        var ctors = type.GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length >= 4 && parameters.Any(p => p.Name == "sqlState"))
            {
                var args = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == "sqlState")
                        args[i] = sqlState;
                    else if (parameters[i].ParameterType == typeof(string))
                        args[i] = parameters[i].Name == "severity" || parameters[i].Name == "invariantSeverity" ? "ERROR" : "duplicate key value violates unique constraint";
                    else
                        args[i] = null;
                }

                return (PostgresException)ctor.Invoke(args);
            }
        }

        throw new InvalidOperationException(
            $"Could not create PostgresException via reflection. Available constructors: " +
            string.Join(", ", type.GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Select(c => $"({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})")));
    }
}
