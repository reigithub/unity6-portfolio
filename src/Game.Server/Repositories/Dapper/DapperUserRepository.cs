using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperUserRepository : IUserRepository
{
    private readonly IDbSession _dbSession;

    public DapperUserRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<UserInfo?> GetByIdAsync(Guid id)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
                     ""Email"", ""AuthType"", ""CreatedAt"", ""UpdatedAt""
              FROM ""User"".""UserInfo"" WHERE ""Id"" = @Id",
            new { Id = id },
            transaction: _dbSession.Transaction);
    }

    public async Task<UserInfo?> GetByUserNameAsync(string displayName)
    {
        return await _dbSession.Connection.QueryFirstOrDefaultAsync<UserInfo>(
            @"SELECT ""Id"", ""UserId"", ""UserName"", ""PasswordHash"", ""TransferPasswordHash"", ""Level"", ""RegisteredAt"", ""LastLoginAt"",
                     ""Email"", ""AuthType"", ""CreatedAt"", ""UpdatedAt""
              FROM ""User"".""UserInfo"" WHERE ""UserName"" = @UserName",
            new { UserName = displayName },
            transaction: _dbSession.Transaction);
    }

    public async Task UpdateAsync(UserInfo user)
    {
        await _dbSession.Connection.ExecuteAsync(
            @"UPDATE ""User"".""UserInfo""
              SET ""UserName"" = @UserName,
                  ""PasswordHash"" = @PasswordHash,
                  ""Level"" = @Level,
                  ""LastLoginAt"" = @LastLoginAt
              WHERE ""Id"" = @Id",
            user,
            transaction: _dbSession.Transaction);
    }
}
