namespace Game.Realtime.Services;

/// <summary>
/// ゲームサーバー接続設定
/// Dedicated Server が未実装の間はデフォルト値を使用
/// </summary>
public class GameServerConfiguration
{
    public string ServerAddress { get; set; } = "localhost";
    public int ServerPort { get; set; } = 7777;
}
