namespace Game.Server.Configuration;

public class RequestSigningSettings
{
    public string SecretKey { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
