using Dapper;
using Game.Library.Shared.Constants;
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
    public static readonly Guid GuestWithTransferPasswordId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    // Public string identifiers (user.UserId — JWT sub と同値、外部公開チャネルで使用)
    public const string User1IdString = "000000000001";
    public const string User2IdString = "000000000002";
    public const string User3IdString = "000000000003";
    public const string GuestUserIdString = "000000000004";
    public const string EmailUserIdString = "000000000005";
    public const string GuestWithTransferPasswordIdString = "000000000006";

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

    public static readonly RequestSigningSettings TestSigningSettings = new()
    {
        SecretKey = "test-signing-secret-key-for-unit-tests",
        Enabled = true,
    };

    public static IOptions<JwtSettings> GetJwtOptions()
    {
        return Options.Create(TestJwtSettings);
    }

    public static IOptions<AuthSettings> GetAuthOptions()
    {
        return Options.Create(TestAuthSettings);
    }

    public static IOptions<RequestSigningSettings> GetSigningOptions()
    {
        return Options.Create(TestSigningSettings);
    }

    public static IDbConnectionFactory CreateConnectionFactory(string connectionString)
    {
        return new TestDbConnectionFactory(connectionString);
    }

    public static IDbSession CreateDbSession(IDbConnectionFactory connectionFactory)
    {
        return new DbSession(connectionFactory);
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
                UserId = "000000000001",
                UserName = "Player1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                Level = 5,
                AuthType = AuthType.Email,
            },
            new UserInfo
            {
                Id = User2Id,
                UserId = "000000000002",
                UserName = "Player2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password2!"),
                Level = 3,
                AuthType = AuthType.Email,
            },
            new UserInfo
            {
                Id = User3Id,
                UserId = "000000000003",
                UserName = "Player3",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password3!"),
                Level = 1,
                AuthType = AuthType.Email,
            },
        };

        foreach (var user in users)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""User"".""UserInfo""
                  (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
                   ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
                   ""EmailVerificationToken"", ""EmailVerificationExpiry"",
                   ""PasswordResetToken"", ""PasswordResetExpiry"",
                   ""FailedLoginAttempts"", ""LockoutEndAt"")
                  VALUES (@Id, @UserId, @UserName, @PasswordHash, @TransferPasswordHash, @Level, @RegisteredAt, @LastLoginAt,
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
            UserId = "000000000004",
            UserName = "Guest_12345678",
            PasswordHash = null,
            Level = 1,
            AuthType = AuthType.Guest,
            DeviceFingerprint = "test-device-fingerprint-0123456789abcdef",
        };
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @UserName, @PasswordHash, @TransferPasswordHash, @Level, @RegisteredAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            guestUser);

        // Email user for unlink tests
        var emailUser = new UserInfo
        {
            Id = EmailUserId,
            UserId = "000000000005",
            UserName = "EmailPlayer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            Level = 2,
            AuthType = AuthType.Email,
            Email = "existing@example.com",
            IsEmailVerified = true,
        };
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @UserName, @PasswordHash, @TransferPasswordHash, @Level, @RegisteredAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            emailUser);

        // Guest user with transfer password for LoginAsync tests
        var guestWithTransferPassword = new UserInfo
        {
            Id = GuestWithTransferPasswordId,
            UserId = "000000000006",
            UserName = "Guest_TransferPW",
            PasswordHash = null,
            TransferPasswordHash = BCrypt.Net.BCrypt.HashPassword("TransferPW1!"),
            Level = 1,
            AuthType = AuthType.Guest,
            DeviceFingerprint = "test-device-fingerprint-transfer-user",
        };
        await connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @UserName, @PasswordHash, @TransferPasswordHash, @Level, @RegisteredAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            guestWithTransferPassword);

        var scores = new[]
        {
            new SurvivorScore { UserId = User1Id, StageId = 1, Score = 5000, ClearTime = 120f, WaveReached = 10, EnemiesDefeated = 50 },
            new SurvivorScore { UserId = User2Id, StageId = 1, Score = 8000, ClearTime = 90f, WaveReached = 15, EnemiesDefeated = 80 },
            new SurvivorScore { UserId = User3Id, StageId = 1, Score = 3000, ClearTime = 60f, WaveReached = 5, EnemiesDefeated = 20 },
        };

        foreach (var score in scores)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Ranking"".""SurvivorScore"" (""UserId"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)",
                score);
        }
    }
}
