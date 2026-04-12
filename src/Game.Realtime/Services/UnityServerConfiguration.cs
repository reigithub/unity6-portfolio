namespace Game.Realtime.Services;

/// <summary>
/// Unity Dedicated Server 接続設定（appsettings "UnityServer" セクション）
/// </summary>
public class UnityServerConfiguration
{
    public string ServerAddress { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 7777;
}
