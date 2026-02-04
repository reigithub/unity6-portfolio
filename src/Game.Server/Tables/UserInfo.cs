using Game.Server.Extensions;

namespace Game.Server.Tables;

public class UserInfo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string UserId { get; set; } = UserIdGenerator.Generate();

    public string UserName { get; set; } = string.Empty;

    public string? PasswordHash { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    public string? Email { get; set; }

    public string AuthType { get; set; } = "Password";

    public string? DeviceFingerprint { get; set; }

    public bool IsEmailVerified { get; set; }

    public string? EmailVerificationToken { get; set; }

    public DateTime? EmailVerificationExpiry { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetExpiry { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
