using Game.Server.Tables;

namespace Game.Server.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<bool> ExistsByUserNameAsync(string displayName);

    Task<UserInfo> CreateUserAsync(UserInfo user);

    Task<UserInfo?> GetByUserNameAsync(string displayName);

    Task<UserInfo?> GetByUserIdStringAsync(string userId);

    Task<UserInfo?> GetByIdAsync(Guid id);

    Task UpdateLastLoginAsync(Guid id, DateTime lastLoginAt);

    Task<UserInfo?> GetByEmailAsync(string email);

    Task<bool> ExistsByEmailAsync(string email);

    Task<UserInfo?> GetByDeviceFingerprintAsync(string fingerprint);

    Task<UserInfo?> GetByEmailVerificationTokenAsync(string token);

    Task<UserInfo?> GetByPasswordResetTokenAsync(string token);

    Task UpdateFailedLoginAsync(Guid id, int attempts, DateTime? lockoutEnd);

    Task ResetFailedLoginAsync(Guid id);

    Task UpdateEmailVerificationAsync(Guid id, bool isVerified);

    Task UpdatePasswordResetTokenAsync(Guid id, string? token, DateTime? expiry);

    Task UpdatePasswordHashAsync(Guid id, string passwordHash);

    Task LinkEmailAsync(Guid id, string email, string passwordHash,
        string? emailVerificationToken, DateTime? emailVerificationExpiry);

    Task UnlinkEmailAsync(Guid id, string deviceFingerprint);
}
