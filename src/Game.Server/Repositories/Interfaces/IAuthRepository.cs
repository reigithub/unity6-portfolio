using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<bool> ExistsByDisplayNameAsync(string displayName);

    Task<UserInfo> CreateUserAsync(UserInfo user);

    Task<UserInfo?> GetByDisplayNameAsync(string displayName);

    Task<UserInfo?> GetByIdAsync(string userId);

    Task UpdateLastLoginAsync(string userId, DateTime lastLoginAt);
}
