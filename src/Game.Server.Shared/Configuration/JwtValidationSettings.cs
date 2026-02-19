using System.ComponentModel.DataAnnotations;

namespace Game.Server.Shared.Configuration;

/// <summary>
/// JWT 検証用設定（Server / Realtime 共通）
/// トークン発行用プロパティは Game.Server の JwtSettings で拡張
/// </summary>
public class JwtValidationSettings
{
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "JWT Secret must be at least 32 characters long.")]
    public string Secret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "Game.Server";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "Game.Client";
}
