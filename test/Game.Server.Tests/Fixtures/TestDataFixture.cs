using Dapper;
using Game.Server.Configuration;
using Game.Server.Database;
using Game.Server.Tables;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Game.Server.Tests.Fixtures;

public static class TestDataFixture
{
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
                Id = "user-1",
                DisplayName = "Player1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                Level = 5,
                AuthType = "Password",
            },
            new UserInfo
            {
                Id = "user-2",
                DisplayName = "Player2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password2!"),
                Level = 3,
                AuthType = "Password",
            },
            new UserInfo
            {
                Id = "user-3",
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
                  (""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"",
                   ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
                   ""EmailVerificationToken"", ""EmailVerificationExpiry"",
                   ""PasswordResetToken"", ""PasswordResetExpiry"",
                   ""FailedLoginAttempts"", ""LockoutEndAt"")
                  VALUES (@Id, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt,
                          @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                          @EmailVerificationToken, @EmailVerificationExpiry,
                          @PasswordResetToken, @PasswordResetExpiry,
                          @FailedLoginAttempts, @LockoutEndAt)",
                user);
        }

        var scores = new[]
        {
            new UserScore { UserId = "user-1", GameMode = "Survivor", StageId = 1, Score = 5000, ClearTime = 120f, WaveReached = 10, EnemiesDefeated = 50 },
            new UserScore { UserId = "user-2", GameMode = "Survivor", StageId = 1, Score = 8000, ClearTime = 90f, WaveReached = 15, EnemiesDefeated = 80 },
            new UserScore { UserId = "user-3", GameMode = "Survivor", StageId = 1, Score = 3000, ClearTime = 60f, WaveReached = 5, EnemiesDefeated = 20 },
            new UserScore { UserId = "user-1", GameMode = "ScoreTimeAttack", StageId = 1, Score = 12000, ClearTime = 45f, EnemiesDefeated = 100 },
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
