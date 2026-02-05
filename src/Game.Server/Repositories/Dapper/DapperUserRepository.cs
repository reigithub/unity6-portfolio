using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserInfo?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
                     ""Email"", ""AuthType"", ""CreatedAt"", ""UpdatedAt""
              FROM ""User"".""UserInfo"" WHERE ""Id"" = @Id",
            new { Id = id });
    }

    public async Task<UserInfo?> GetByUserNameAsync(string displayName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
                     ""Email"", ""AuthType"", ""CreatedAt"", ""UpdatedAt""
              FROM ""User"".""UserInfo"" WHERE ""UserName"" = @UserName",
            new { UserName = displayName });
    }

    public async Task UpdateAsync(UserInfo user)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""UserName"" = @UserName,
                  ""PasswordHash"" = @PasswordHash,
                  ""Level"" = @Level,
                  ""LastLoginAt"" = @LastLoginAt
              WHERE ""Id"" = @Id",
            user);
    }
}
