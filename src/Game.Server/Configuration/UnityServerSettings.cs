using System.ComponentModel.DataAnnotations;

namespace Game.Server.Configuration;

/// <summary>
/// Unity Dedicated Server 接続認証用 HMAC 署名シークレットキー設定。
/// SurvivorNetworkAuthenticator と同一のシークレットを使用すること。
/// DS 間通信（登録・ハートビート・セッション管理）にも共用する。
/// </summary>
public class UnityServerSettings
{
    /// <summary>
    /// HMAC-SHA256 署名用のシークレットキー（32文字以上）。
    /// 本番環境では User Secrets または環境変数で設定すること。
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "UnityServer SecretKey must be at least 32 characters long.")]
    public string SecretKey { get; set; } = "";
}
