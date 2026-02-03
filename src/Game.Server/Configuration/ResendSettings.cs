namespace Game.Server.Configuration;

public class ResendSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Game Server";
}
