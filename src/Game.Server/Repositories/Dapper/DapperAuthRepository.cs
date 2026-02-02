using Dapper;
using Game.Server.Database;
using Game.Server.Repositories.Interfaces;
using Game.Server.Tables;

namespace Game.Server.Repositories.Dapper;

public class DapperAuthRepository : IAuthRepository
{
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
            @"INSERT INTO ""User"".""UserInfo"" (""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"")
              VALUES (@Id, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt)",
            user);
        return user;
    }

    public async Task<UserInfo?> GetByDisplayNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
              FROM ""User"".""UserInfo"" WHERE ""DisplayName"" = @DisplayName",
            new { DisplayName = displayName });
    }

    public async Task<UserInfo?> GetByIdAsync(string userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
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
}
