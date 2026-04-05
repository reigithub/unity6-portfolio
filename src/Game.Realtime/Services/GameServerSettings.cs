using System.ComponentModel.DataAnnotations;

namespace Game.Realtime.Services;

/// <summary>
/// Game.Server への HTTP 接続設定。
/// </summary>
public class GameServerSettings
{
    /// <summary>
    /// Game.Server のベース URL（例: <c>http://localhost:5000</c>）
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = "";
}
