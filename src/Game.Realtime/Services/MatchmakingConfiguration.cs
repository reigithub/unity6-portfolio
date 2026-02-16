namespace Game.Realtime.Services;

/// <summary>
/// マッチメイキング設定
/// </summary>
public class MatchmakingConfiguration
{
    public Dictionary<string, GameModeConfig> GameModes { get; set; } = new()
    {
        ["survival"] = new GameModeConfig { MatchSize = 4 },
    };
}

/// <summary>
/// ゲームモード設定
/// </summary>
public class GameModeConfig
{
    public int MatchSize { get; set; } = 4;
}
