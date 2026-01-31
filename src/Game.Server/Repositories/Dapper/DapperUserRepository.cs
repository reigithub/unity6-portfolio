using Dapper;
using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserEntity?> GetByIdAsync(string userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserEntity>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
              FROM ""Users"" WHERE ""Id"" = @Id",
            new { Id = userId });
    }

    public async Task<UserEntity?> GetByDisplayNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserEntity>(
            @"SELECT ""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt""
              FROM ""Users"" WHERE ""DisplayName"" = @DisplayName",
            new { DisplayName = displayName });
    }

    public async Task UpdateAsync(UserEntity user)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""Users""
              SET ""DisplayName"" = @DisplayName,
                  ""PasswordHash"" = @PasswordHash,
                  ""Level"" = @Level,
                  ""CreatedAt"" = @CreatedAt,
                  ""LastLoginAt"" = @LastLoginAt
              WHERE ""Id"" = @Id",
            user);
    }
}
