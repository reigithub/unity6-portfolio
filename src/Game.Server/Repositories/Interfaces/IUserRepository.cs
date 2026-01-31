using Game.Server.Entities;

namespace Game.Server.Repositories.Interfaces;

public interface IUserRepository
{
    Task<UserEntity?> GetByIdAsync(string userId);

    Task<UserEntity?> GetByDisplayNameAsync(string displayName);

    Task UpdateAsync(UserEntity user);
}
