using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<bool> ExistsByDisplayNameAsync(string displayName);

    Task<UserInfo> CreateUserAsync(UserInfo user);

    Task<UserInfo?> GetByDisplayNameAsync(string displayName);

    Task<UserInfo?> GetByIdAsync(string userId);

    Task UpdateLastLoginAsync(string userId, DateTime lastLoginAt);

    Task<UserInfo?> GetByEmailAsync(string email);

    Task<bool> ExistsByEmailAsync(string email);

    Task<UserInfo?> GetByDeviceFingerprintAsync(string fingerprint);

    Task<UserInfo?> GetByEmailVerificationTokenAsync(string token);

    Task<UserInfo?> GetByPasswordResetTokenAsync(string token);

    Task UpdateFailedLoginAsync(string userId, int attempts, DateTime? lockoutEnd);

    Task ResetFailedLoginAsync(string userId);

    Task UpdateEmailVerificationAsync(string userId, bool isVerified);

    Task UpdatePasswordResetTokenAsync(string userId, string? token, DateTime? expiry);

    Task UpdatePasswordHashAsync(string userId, string passwordHash);
}
