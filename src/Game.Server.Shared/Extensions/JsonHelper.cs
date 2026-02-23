using System.Text.Json;

namespace Game.Server.Shared.Extensions;

/// <summary>
/// JSON シリアライズ/デシリアライズの共通ヘルパー。
/// JsonSerializerOptions の一元管理とエラーハンドリングを提供する。
/// </summary>
public static class JsonHelper
{
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// JSON デシリアライズを安全に実行する。
    /// JsonException 発生時はログ出力して null を返す。
    /// </summary>
    public static T? TryDeserialize<T>(string json, ILogger logger, string context) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize {Context}", context);
            return null;
        }
    }
}
