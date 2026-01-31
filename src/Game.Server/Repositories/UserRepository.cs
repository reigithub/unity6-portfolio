using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Game.Server.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserEntity?> GetByIdAsync(string userId)
    {
        return await _dbContext.Users.FindAsync(userId);
    }

    public async Task<UserEntity?> GetByDisplayNameAsync(string displayName)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.DisplayName == displayName);
    }

    public async Task UpdateAsync(UserEntity user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }
}
