using Dapper;
using Game.Server.Database;
using Game.Server.Repositories.Interfaces;
using Game.Server.Tables;

namespace Game.Server.Repositories.Dapper;

public class DapperAuthRepository : IAuthRepository
{
    private const string SelectColumns =
        @"""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"",
          ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
          ""EmailVerificationToken"", ""EmailVerificationExpiry"",
          ""PasswordResetToken"", ""PasswordResetExpiry"",
          ""FailedLoginAttempts"", ""LockoutEndAt""";

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsByDisplayNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            @"SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ""User"".""UserInfo"" WHERE ""DisplayName"" = @DisplayName
              ) THEN 1 ELSE 0 END",
            new { DisplayName = displayName });
    }

    public async Task<UserInfo> CreateUserAsync(UserInfo user)
    {
        using var connection = _connectionFactory.CreateConnection();
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
        return user;
    }

    public async Task<UserInfo?> GetByDisplayNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""DisplayName"" = @DisplayName",
            new { DisplayName = displayName });
    }

    public async Task<UserInfo?> GetByIdAsync(string userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""Id"" = @Id",
            new { Id = userId });
    }

    public async Task UpdateLastLoginAsync(string userId, DateTime lastLoginAt)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo"" SET ""LastLoginAt"" = @LastLoginAt WHERE ""Id"" = @Id",
            new { Id = userId, LastLoginAt = lastLoginAt });
    }

    public async Task<UserInfo?> GetByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email",
            new { Email = email });
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            @"SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email
              ) THEN 1 ELSE 0 END",
            new { Email = email });
    }

    public async Task<UserInfo?> GetByDeviceFingerprintAsync(string fingerprint)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo""
              WHERE ""DeviceFingerprint"" = @DeviceFingerprint AND ""AuthType"" = 'Guest'",
            new { DeviceFingerprint = fingerprint });
    }

    public async Task<UserInfo?> GetByEmailVerificationTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""EmailVerificationToken"" = @Token",
            new { Token = token });
    }

    public async Task<UserInfo?> GetByPasswordResetTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""PasswordResetToken"" = @Token",
            new { Token = token });
    }

    public async Task UpdateFailedLoginAsync(string userId, int attempts, DateTime? lockoutEnd)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""FailedLoginAttempts"" = @Attempts, ""LockoutEndAt"" = @LockoutEnd
              WHERE ""Id"" = @Id",
            new { Id = userId, Attempts = attempts, LockoutEnd = lockoutEnd });
    }

    public async Task ResetFailedLoginAsync(string userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""FailedLoginAttempts"" = 0, ""LockoutEndAt"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = userId });
    }

    public async Task UpdateEmailVerificationAsync(string userId, bool isVerified)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""IsEmailVerified"" = @IsVerified,
                  ""EmailVerificationToken"" = NULL,
                  ""EmailVerificationExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = userId, IsVerified = isVerified });
    }

    public async Task UpdatePasswordResetTokenAsync(string userId, string? token, DateTime? expiry)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""PasswordResetToken"" = @Token,
                  ""PasswordResetExpiry"" = @Expiry
              WHERE ""Id"" = @Id",
            new { Id = userId, Token = token, Expiry = expiry });
    }

    public async Task UpdatePasswordHashAsync(string userId, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""PasswordHash"" = @PasswordHash,
                  ""PasswordResetToken"" = NULL,
                  ""PasswordResetExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = userId, PasswordHash = passwordHash });
    }

    public async Task LinkEmailAsync(string userId, string email, string passwordHash, string displayName,
        string? emailVerificationToken, DateTime? emailVerificationExpiry)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""AuthType"" = 'Email',
                  ""Email"" = @Email,
                  ""PasswordHash"" = @PasswordHash,
                  ""DisplayName"" = @DisplayName,
                  ""DeviceFingerprint"" = NULL,
                  ""EmailVerificationToken"" = @EmailVerificationToken,
                  ""EmailVerificationExpiry"" = @EmailVerificationExpiry
              WHERE ""Id"" = @Id",
            new
            {
                Id = userId,
                Email = email,
                PasswordHash = passwordHash,
                DisplayName = displayName,
                EmailVerificationToken = emailVerificationToken,
                EmailVerificationExpiry = emailVerificationExpiry
            });
    }

    public async Task UnlinkEmailAsync(string userId, string deviceFingerprint)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""AuthType"" = 'Guest',
                  ""Email"" = NULL,
                  ""PasswordHash"" = NULL,
                  ""DeviceFingerprint"" = @DeviceFingerprint,
                  ""IsEmailVerified"" = FALSE,
                  ""EmailVerificationToken"" = NULL,
                  ""EmailVerificationExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = userId, DeviceFingerprint = deviceFingerprint });
    }
}
