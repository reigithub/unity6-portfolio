using Game.Server.Entities;

namespace Game.Server.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<bool> ExistsByDisplayNameAsync(string displayName);

    Task<UserEntity> CreateUserAsync(UserEntity user);

    Task<UserEntity?> GetByDisplayNameAsync(string displayName);

    Task<UserEntity?> GetByIdAsync(string userId);

    Task UpdateLastLoginAsync(string userId, DateTime lastLoginAt);
}
