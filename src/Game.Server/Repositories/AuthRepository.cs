using Dapper;
using Game.Server.Database;
using Game.Server.Repositories.Interfaces;
using Game.Server.Tables;

namespace Game.Server.Repositories;

public class AuthRepository : IAuthRepository
{
    private const string SelectColumns =
        @"""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
          ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
          ""EmailVerificationToken"", ""EmailVerificationExpiry"",
          ""PasswordResetToken"", ""PasswordResetExpiry"",
          ""FailedLoginAttempts"", ""LockoutEndAt"",
          ""CreatedAt"", ""UpdatedAt""";

    private readonly IDbSession _dbSession;

    public AuthRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> ExistsByUserNameAsync(string displayName)
    {
        return await _dbSession.Connection.ExecuteScalarAsync<bool>(
            @"SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ""User"".""UserInfo"" WHERE ""UserName"" = @UserName
              ) THEN 1 ELSE 0 END",
            new { UserName = displayName },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo> CreateUserAsync(UserInfo user)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"INSERT INTO ""User"".""UserInfo""
              (""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
               ""Email"", ""AuthType"", ""DeviceFingerprint"", ""IsEmailVerified"",
               ""EmailVerificationToken"", ""EmailVerificationExpiry"",
               ""PasswordResetToken"", ""PasswordResetExpiry"",
               ""FailedLoginAttempts"", ""LockoutEndAt"")
              VALUES (@Id, @UserId, @UserName, @PasswordHash, @Level, @RegisteredAt, @LastLoginAt,
                      @Email, @AuthType, @DeviceFingerprint, @IsEmailVerified,
                      @EmailVerificationToken, @EmailVerificationExpiry,
                      @PasswordResetToken, @PasswordResetExpiry,
                      @FailedLoginAttempts, @LockoutEndAt)",
            user,
            transaction: _dbSession.Transaction);
        return user;
    }

    public async Task<UserInfo?> GetByUserNameAsync(string displayName)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""UserName"" = @UserName",
            new { UserName = displayName },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByUserIdStringAsync(string userId)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""UserId"" = @UserId",
            new { UserId = userId },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByIdAsync(Guid id)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""Id"" = @Id",
            new { Id = id },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdateLastLoginAsync(Guid id, DateTime lastLoginAt)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo"" SET ""LastLoginAt"" = @LastLoginAt WHERE ""Id"" = @Id",
            new { Id = id, LastLoginAt = lastLoginAt },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByEmailAsync(string email)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email",
            new { Email = email },
            transaction: _dbSession.Transaction);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _dbSession.Connection.ExecuteScalarAsync<bool>(
            @"SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ""User"".""UserInfo"" WHERE ""Email"" = @Email
              ) THEN 1 ELSE 0 END",
            new { Email = email },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByDeviceFingerprintAsync(string fingerprint)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo""
              WHERE ""DeviceFingerprint"" = @DeviceFingerprint AND ""AuthType"" = 'Guest'",
            new { DeviceFingerprint = fingerprint },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByEmailVerificationTokenAsync(string token)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""EmailVerificationToken"" = @Token",
            new { Token = token },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByPasswordResetTokenAsync(string token)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            $@"SELECT {SelectColumns}
              FROM ""User"".""UserInfo"" WHERE ""PasswordResetToken"" = @Token",
            new { Token = token },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdateFailedLoginAsync(Guid id, int attempts, DateTime? lockoutEnd)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""FailedLoginAttempts"" = @Attempts, ""LockoutEndAt"" = @LockoutEnd
              WHERE ""Id"" = @Id",
            new { Id = id, Attempts = attempts, LockoutEnd = lockoutEnd },
            transaction: _dbSession.Transaction);
    }

    public async Task ResetFailedLoginAsync(Guid id)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""FailedLoginAttempts"" = 0, ""LockoutEndAt"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = id },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdateEmailVerificationAsync(Guid id, bool isVerified)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""IsEmailVerified"" = @IsVerified,
                  ""EmailVerificationToken"" = NULL,
                  ""EmailVerificationExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = id, IsVerified = isVerified },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdatePasswordResetTokenAsync(Guid id, string? token, DateTime? expiry)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""PasswordResetToken"" = @Token,
                  ""PasswordResetExpiry"" = @Expiry
              WHERE ""Id"" = @Id",
            new { Id = id, Token = token, Expiry = expiry },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdatePasswordHashAsync(Guid id, string passwordHash)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""PasswordHash"" = @PasswordHash,
                  ""PasswordResetToken"" = NULL,
                  ""PasswordResetExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = id, PasswordHash = passwordHash },
            transaction: _dbSession.Transaction);
    }

    public async Task LinkEmailAsync(Guid id, string email, string passwordHash,
        string? emailVerificationToken, DateTime? emailVerificationExpiry)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""AuthType"" = 'Email',
                  ""Email"" = @Email,
                  ""PasswordHash"" = @PasswordHash,
                  ""DeviceFingerprint"" = NULL,
                  ""EmailVerificationToken"" = @EmailVerificationToken,
                  ""EmailVerificationExpiry"" = @EmailVerificationExpiry
              WHERE ""Id"" = @Id",
            new
            {
                Id = id,
                Email = email,
                PasswordHash = passwordHash,
                EmailVerificationToken = emailVerificationToken,
                EmailVerificationExpiry = emailVerificationExpiry
            },
            transaction: _dbSession.Transaction);
    }

    public async Task UnlinkEmailAsync(Guid id, string deviceFingerprint)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""AuthType"" = 'Guest',
                  ""Email"" = NULL,
                  ""PasswordHash"" = NULL,
                  ""DeviceFingerprint"" = @DeviceFingerprint,
                  ""IsEmailVerified"" = FALSE,
                  ""EmailVerificationToken"" = NULL,
                  ""EmailVerificationExpiry"" = NULL
              WHERE ""Id"" = @Id",
            new { Id = id, DeviceFingerprint = deviceFingerprint },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdateTransferPasswordHashAsync(Guid id, string? transferPasswordHash)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""TransferPasswordHash"" = @TransferPasswordHash
              WHERE ""Id"" = @Id",
            new { Id = id, TransferPasswordHash = transferPasswordHash },
            transaction: _dbSession.Transaction);
    }
}
