namespace Game.Server.Tables;

public class UserExternalIdentity
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string ProviderUserId { get; set; } = string.Empty;

    public string? ProviderData { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}
