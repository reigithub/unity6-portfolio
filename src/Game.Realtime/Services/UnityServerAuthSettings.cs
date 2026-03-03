namespace Game.Realtime.Services;

/// <summary>
/// Unity Dedicated Server 接続認証用 HMAC 署名シークレットキー設定。
/// SurvivorNetworkAuthenticator と同一のシークレットを使用すること。
/// </summary>
public class UnityServerAuthSettings
{
    public string SecretKey { get; set; } = "";
}
