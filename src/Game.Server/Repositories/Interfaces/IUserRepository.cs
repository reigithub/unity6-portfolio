using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IUserRepository
{
    Task<UserInfo?> GetByIdAsync(Guid id);

    Task<UserInfo?> GetByDisplayNameAsync(string displayName);

    Task UpdateAsync(UserInfo user);
}
