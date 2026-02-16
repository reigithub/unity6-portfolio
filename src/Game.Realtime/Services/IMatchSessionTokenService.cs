namespace Game.Realtime.Services;

/// <summary>
/// マッチセッショントークンサービスインターフェース
/// マッチ成立後に短命トークンを発行し、Dedicated Server 接続認証に使用
/// </summary>
public interface IMatchSessionTokenService
{
    /// <summary>
    /// セッショントークンを発行（Valkey に保存）
    /// </summary>
    Task<string> IssueTokenAsync(string userId, string matchId, TimeSpan? expiry = null);

    /// <summary>
    /// セッショントークンを検証
    /// </summary>
    Task<SessionTokenInfo?> ValidateTokenAsync(string token);

    /// <summary>
    /// セッショントークンを無効化
    /// </summary>
    Task RevokeTokenAsync(string token);
}

/// <summary>
/// セッショントークン情報
/// </summary>
public class SessionTokenInfo
{
    public string UserId { get; init; } = string.Empty;

    public string MatchId { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }
}
