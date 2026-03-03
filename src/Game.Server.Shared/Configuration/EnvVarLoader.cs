namespace Game.Server.Shared.Configuration;

/// <summary>
/// docker/game-server/.env を読み取り、Environment.SetEnvironmentVariable で注入する。
/// ASP.NET Core の Configuration 階層で環境変数が自動取得される。
/// Docker 環境では環境変数が直接設定されるため、このメソッドは no-op になる。
/// </summary>
public static class EnvVarLoader
{
    /// <summary>
    /// .env の Shell-style キーを ASP.NET Core のコンフィグキーにマッピング。
    /// </summary>
    private static readonly (string EnvKey, string ConfigKey)[] _keyMappings =
    {
        ("JWT_SECRET", "Jwt__Secret"),
        ("UNITY_SERVER_AUTH_SESSION_SECRET", "UnityServerAuth__SecretKey"),
    };

    /// <summary>
    /// .env ファイルを検索して環境変数に注入する。
    /// 既に設定済みの環境変数は上書きしない（Docker Compose での設定が優先）。
    /// </summary>
    public static void Load()
    {
        var envPath = FindEnvFile();
        if (envPath == null) return;

        var envVars = Parse(envPath).ToDictionary(x => x.Key, x => x.Value);

        // raw キーを環境変数に設定
        foreach (var (key, value) in envVars)
        {
            if (Environment.GetEnvironmentVariable(key) == null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        // Shell-style → ASP.NET Core config キーにマッピング
        foreach (var (envKey, configKey) in _keyMappings)
        {
            if (Environment.GetEnvironmentVariable(configKey) == null &&
                envVars.TryGetValue(envKey, out var val))
            {
                Environment.SetEnvironmentVariable(configKey, val);
            }
        }
    }

    private static string? FindEnvFile()
    {
        // dotnet run のワーキングディレクトリから親方向に探索
        // src/Game.Server/ or src/Game.Realtime/ → ../../docker/game-server/.env
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 5; i++)
        {
            var candidate = Path.Combine(dir, "docker", "game-server", ".env");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private static IEnumerable<(string Key, string Value)> Parse(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0) continue;

            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();

            // 引用符の除去
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            yield return (key, value);
        }
    }
}
