using Dapper;
using Game.Server.Configuration;
using Game.Server.Database;
using Game.Server.Tables;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Game.Server.Tests.Fixtures;

public static class TestDataFixture
{
    // Fixed Guids for test data
    public static readonly Guid User1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid User2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid User3Id = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid GuestUserId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid EmailUserId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    public static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-must-be-at-least-32-characters-long!",
        Issuer = "Game.Server",
        Audience = "Game.Client",
        ExpirationMinutes = 60,
        RefreshExpirationDays = 30,
    };

    public static readonly AuthSettings TestAuthSettings = new()
    {
        MaxFailedLoginAttempts = 5,
        LockoutMinutes = 15,
        EmailVerificationExpiryHours = 24,
        PasswordResetExpiryMinutes = 30,
    };

    public static IOptions<JwtSettings> GetJwtOptions()
    {
        return Options.Create(TestJwtSettings);
    }

    public static IOptions<AuthSettings> GetAuthOptions()
    {
        return Options.Create(TestAuthSettings);
    }

    public static IDbConnectionFactory CreateConnectionFactory(string connectionString)
    {
        return new TestDbConnectionFactory(connectionString);
    }

    public static async Task SeedTestDataAsync(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var users = new[]
        {
            new UserInfo
            {
                Id = User1Id,
                UserId = "testuser01",
                DisplayName = "Player1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                Level = 5,
                AuthType = "Password",
            },
            new UserInfo
            {
                Id = User2Id,
                UserId = "testuser02",
                DisplayName = "Player2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password2!"),
                Level = 3,
                AuthType = "Password",
            },
            new UserInfo
            {
                Id = User3Id,
                UserId = "testuser03",
                DisplayName = "Player3",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password3!"),
                Level = 1,
                AuthType = "Password",
            },
        };

        foreach (var user in users)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""User"".""UserInfo""
                  (""Id"", ""UserId"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"",
                   ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
                   ""EmailVerificationToken"", ""EmailVerificationExpiry"",
                   ""PasswordResetToken"", ""PasswordResetExpiry"",
                   ""FailedLoginAttempts"", ""LockoutEndAt"")
                  VALUES (@Id, @UserId, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt,
                          @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                          @EmailVerificationToken, @EmailVerificationExpiry,
                          @PasswordResetToken, @PasswordResetExpiry,
                          @FailedLoginAttempts, @LockoutEndAt)",
                user);
        }

        // Guest user for account linking tests
        var guestUser = new UserInfo
        {
            Id = GuestUserId,
            UserId = "guestuser01",
            DisplayName = "Guest_12345678",
            PasswordHash = null,
            Level = 1,
            AuthType = "Guest",
            DeviceFingerprint = "test-device-fingerprint-0123456789abcdef",
        };
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            guestUser);

        // Email user for unlink tests
        var emailUser = new UserInfo
        {
            Id = EmailUserId,
            UserId = "emailuser01",
            DisplayName = "EmailPlayer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            Level = 2,
            AuthType = "Email",
            Email = "existing@example.com",
            IsEmailVerified = true,
        };
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            emailUser);

        var scores = new[]
        {
            new UserScore { UserId = User1Id, GameMode = "Survivor", StageId = 1, Score = 5000, ClearTime = 120f, WaveReached = 10, EnemiesDefeated = 50 },
            new UserScore { UserId = User2Id, GameMode = "Survivor", StageId = 1, Score = 8000, ClearTime = 90f, WaveReached = 15, EnemiesDefeated = 80 },
            new UserScore { UserId = User3Id, GameMode = "Survivor", StageId = 1, Score = 3000, ClearTime = 60f, WaveReached = 5, EnemiesDefeated = 20 },
            new UserScore { UserId = User1Id, GameMode = "ScoreTimeAttack", StageId = 1, Score = 12000, ClearTime = 45f, EnemiesDefeated = 100 },
        };

        foreach (var score in scores)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""User"".""UserScore"" (""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @GameMode, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)",
                score);
        }
    }
}
