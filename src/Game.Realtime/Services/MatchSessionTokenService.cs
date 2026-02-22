using System.Security.Cryptography;
using System.Text.Json;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのマッチセッショントークンサービス実装
/// </summary>
public class MatchSessionTokenService : IMatchSessionTokenService
{
    private const string KeyPrefix = "session:token:";
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MatchSessionTokenService> _logger;

    public MatchSessionTokenService(IConnectionMultiplexer redis, ILogger<MatchSessionTokenService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> IssueTokenAsync(string userId, string matchId, TimeSpan? expiry = null)
    {
        var token = GenerateSecureToken();
        var tokenExpiry = expiry ?? DefaultExpiry;
        var expiresAt = DateTimeOffset.UtcNow.Add(tokenExpiry);

        var info = new SessionTokenInfo
        {
            UserId = userId,
            MatchId = matchId,
            ExpiresAt = expiresAt,
        };

        var db = _redis.GetDatabase();
        var serialized = JsonSerializer.Serialize(info);
        await db.StringSetAsync($"{KeyPrefix}{token}", serialized, tokenExpiry);

        _logger.LogInformation(
            "Issued session token for user {UserId}, match {MatchId}, expires at {ExpiresAt}",
            userId,
            matchId,
            expiresAt);

        return token;
    }

    public async Task<SessionTokenInfo?> ValidateTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"{KeyPrefix}{token}");

        if (value.IsNullOrEmpty)
        {
            _logger.LogDebug("Session token not found or expired: {Token}", token[..Math.Min(8, token.Length)]);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SessionTokenInfo>(value!);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize session token: {Token}", token[..Math.Min(8, token.Length)]);
            return null;
        }
    }

    public async Task RevokeTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}{token}");

        _logger.LogInformation("Revoked session token: {Token}", token[..Math.Min(8, token.Length)]);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
