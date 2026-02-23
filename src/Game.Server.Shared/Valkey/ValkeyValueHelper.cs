using StackExchange.Redis;

namespace Game.Server.Shared.Valkey;

/// <summary>
/// RedisValue / Redis ハッシュ辞書からの型安全な値取得ヘルパー。
/// </summary>
public static class ValkeyValueHelper
{
    // --- Dictionary<string, RedisValue> 向け ---

    public static int GetInt(
        this Dictionary<string, RedisValue> dict, string key, int defaultValue = 0)
    {
        return int.TryParse(dict.GetValueOrDefault(key), out var result)
            ? result : defaultValue;
    }

    public static long GetLong(
        this Dictionary<string, RedisValue> dict, string key, long defaultValue = 0)
    {
        return long.TryParse(dict.GetValueOrDefault(key), out var result)
            ? result : defaultValue;
    }

    public static string GetString(
        this Dictionary<string, RedisValue> dict, string key, string defaultValue = "")
    {
        var value = dict.GetValueOrDefault(key);
        return value.HasValue ? value.ToString() : defaultValue;
    }

    public static bool GetBool(
        this Dictionary<string, RedisValue> dict, string key, string trueValue = "1")
    {
        return dict.GetValueOrDefault(key) == trueValue;
    }

    // --- 単一 RedisValue 向け ---
    public static int ToInt(this RedisValue value, int defaultValue = 0)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}
