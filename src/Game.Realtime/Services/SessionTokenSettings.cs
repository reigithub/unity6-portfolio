namespace Game.Realtime.Services;

/// <summary>
/// セッショントークンの HMAC 署名に使用する共有シークレットキー設定。
/// Dedicated Server と同一のシークレットを使用すること。
/// </summary>
public class SessionTokenSettings
{
    public string SecretKey { get; set; } = "";
}
