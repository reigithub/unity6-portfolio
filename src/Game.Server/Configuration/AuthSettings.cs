namespace Game.Server.Configuration;

public class AuthSettings
{
    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public int EmailVerificationExpiryHours { get; set; } = 24;

    public int PasswordResetExpiryMinutes { get; set; } = 30;
}
