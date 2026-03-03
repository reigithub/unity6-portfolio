using System.Text;
using Game.Library.Shared.RequestSigning;
using Game.Server.Shared.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// HMAC + Valkey ハイブリッドのマッチセッショントークンサービス。
/// トークン自体に HMAC 署名を埋め込み、Dedicated Server は HMAC のみで検証可能。
/// Valkey には引き続き保存し、失効管理やトークン追跡に使用する。
/// </summary>
public class MatchSessionTokenService : IMatchSessionTokenService
{
    private const string KeyPrefix = "session:token:";

    private readonly IConnectionMultiplexer _redis;
    private readonly byte[] _secretKey;
    private readonly ILogger<MatchSessionTokenService> _logger;

    public MatchSessionTokenService(
        IConnectionMultiplexer redis,
        IOptions<UnityServerAuthSettings> settings,
        ILogger<MatchSessionTokenService> logger)
    {
        _redis = redis;
        _secretKey = Encoding.UTF8.GetBytes(settings.Value.SecretKey);
        _logger = logger;
    }

    public async Task<string> IssueTokenAsync(string userId, string matchId, TimeSpan? expiry = null)
    {
        var tokenExpiry = expiry ?? SessionTokenHelper.DefaultExpiry;
        var expiresAt = DateTimeOffset.UtcNow.Add(tokenExpiry);

        // HMAC 署名付きトークン生成
        var token = SessionTokenHelper.CreateToken(_secretKey, userId, matchId);

        // Valkey にも保存（失効管理・追跡用）
        var info = new SessionTokenInfo
        {
            UserId = userId,
            MatchId = matchId,
            ExpiresAt = expiresAt,
        };

        var db = _redis.GetDatabase();
        var serialized = JsonHelper.Serialize(info);
        await db.StringSetAsync($"{KeyPrefix}{token}", serialized, tokenExpiry);

        _logger.LogInformation(
            "Issued HMAC session token for user {UserId}, match {MatchId}",
            userId, matchId);

        return token;
    }

    public async Task<SessionTokenInfo?> ValidateTokenAsync(string token)
    {
        // Step 1: HMAC 署名検証（Valkey 不要、ローカルで完結）
        var parsed = SessionTokenHelper.ParseAndVerify(token, _secretKey);
        if (parsed == null)
        {
            _logger.LogDebug("HMAC verification failed for token");
            return null;
        }

        // Step 2: Valkey で失効チェック（revoke 済み or 期限切れ → null）
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"{KeyPrefix}{token}");
        if (value.IsNullOrEmpty)
        {
            _logger.LogDebug("Token revoked or expired in Valkey");
            return null;
        }

        return new SessionTokenInfo
        {
            UserId = parsed.UserId,
            MatchId = parsed.MatchId,
            ExpiresAt = parsed.IssuedAt.Add(SessionTokenHelper.DefaultExpiry),
        };
    }

    public async Task RevokeTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}{token}");

        _logger.LogInformation("Revoked session token: {Token}", token[..Math.Min(8, token.Length)]);
    }
}
