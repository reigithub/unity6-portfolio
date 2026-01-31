namespace Game.Server.Configuration;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Game.Server";

    public string Audience { get; set; } = "Game.Client";

    public int ExpirationMinutes { get; set; } = 60;

    public int RefreshExpirationDays { get; set; } = 30;
}
