namespace Game.Server.Tables;

public class UserExternalIdentity
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderUserId { get; set; } = string.Empty;

    public string? ProviderData { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
