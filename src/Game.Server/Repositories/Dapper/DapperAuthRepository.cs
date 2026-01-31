using Dapper;
using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;

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
                SELECT 1 FROM ""Users"" WHERE ""DisplayName"" = @DisplayName
              ) THEN 1 ELSE 0 END",
            new { DisplayName = displayName });
    }

    public async Task<UserEntity> CreateUserAsync(UserEntity user)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO ""Users"" (""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"")
              VALUES (@Id, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt)",
            user);
        return user;
    }

    public async Task<UserEntity?> GetByDisplayNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserEntity>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
              FROM ""Users"" WHERE ""DisplayName"" = @DisplayName",
            new { DisplayName = displayName });
    }

    public async Task<UserEntity?> GetByIdAsync(string userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserEntity>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
              FROM ""Users"" WHERE ""Id"" = @Id",
            new { Id = userId });
    }

    public async Task UpdateLastLoginAsync(string userId, DateTime lastLoginAt)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""Users"" SET ""LastLoginAt"" = @LastLoginAt WHERE ""Id"" = @Id",
            new { Id = userId, LastLoginAt = lastLoginAt });
    }
}
