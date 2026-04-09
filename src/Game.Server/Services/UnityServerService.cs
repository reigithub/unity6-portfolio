using System.Text;
using Game.Library.Shared.Dto;
using Game.Library.Shared.RequestSigning;
using Game.Server.Configuration;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Server.Services;

/// <summary>
/// Unity Dedicated Server 接続用セッショントークン発行・検証サービス。
/// HMAC 署名 + Valkey ハイブリッド方式で SP/MP 共通のトークンを発行する。
/// トークン自体に HMAC 署名を埋め込み、Dedicated Server は HMAC のみで検証可能。
/// Valkey には引き続き保存し、失効管理やトークン追跡に使用する。
/// stageId が 0 より大きい場合は SessionAssignmentService 経由で DS へのセッション割り当ても行う。
/// </summary>
public class UnityServerService : IUnityServerService
{
    private const string KeyPrefix = "session:token:";

    private readonly IConnectionMultiplexer _redis;
    private readonly byte[] _secretKey;
    private readonly ISessionAssignmentService _sessionAssignment;
    private readonly ILogger<UnityServerService> _logger;

    public UnityServerService(
        IConnectionMultiplexer redis,
        IOptions<UnityServerSettings> settings,
        ISessionAssignmentService sessionAssignment,
        ILogger<UnityServerService> logger)
    {
        _redis = redis;
        _secretKey = Encoding.UTF8.GetBytes(settings.Value.SecretKey);
        _sessionAssignment = sessionAssignment;
        _logger = logger;
    }

    /// <summary>
    /// 指定ユーザーに対してセッショントークンを発行する。
    /// SP クライアント用に一意の matchId を UUID ベースで生成する。
    /// stageId が 0 より大きい場合は DS へのセッション割り当てを実行する。
    /// </summary>
    /// <param name="userId">トークン発行対象のユーザーID。</param>
    /// <param name="matchId">マッチID。null の場合は自動生成（SP 用）。</param>
    /// <param name="stageId">ステージID。0 の場合は DS 割り当てをスキップ。</param>
    /// <param name="expectedPlayers">期待プレイヤー数。DS 割り当て時に渡す。</param>
    /// <returns>発行されたトークンとセッション名を含むレスポンス。</returns>
    public async Task<UnityServerAuthResponse> IssueTokenAsync(
        string userId, string matchId = null, int stageId = 0, int expectedPlayers = 1)
    {
        matchId ??= $"sp-{Guid.NewGuid():N}";
        var tokenExpiry = SessionTokenHelper.DefaultExpiry;
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
        await db.StringSetAsync($"{KeyPrefix}{userId}:{matchId}", serialized, tokenExpiry);

        _logger.LogInformation(
            "Issued HMAC session token for user {UserId}, match {MatchId}", userId, matchId);

        // DS セッション割り当て（stageId が指定された場合のみ実行）
        if (stageId > 0)
        {
            await _sessionAssignment.AssignSessionAsync(matchId, stageId, expectedPlayers);
        }

        return new UnityServerAuthResponse
        {
            Token = token,
            SessionName = matchId,
        };
    }

    /// <summary>
    /// セッショントークンを検証し、ペイロードを返す。
    /// HMAC 署名検証 + Valkey 失効チェックを行う。
    /// </summary>
    /// <param name="token">検証するトークン文字列。</param>
    /// <returns>検証成功時はパース結果、失敗または失効済みの場合は null。</returns>
    public async Task<SessionTokenParseResult?> ValidateTokenAsync(string token)
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
        var value = await db.StringGetAsync($"{KeyPrefix}{parsed.UserId}:{parsed.MatchId}");
        if (value.IsNullOrEmpty)
        {
            _logger.LogDebug("Token revoked or expired in Valkey");
            return null;
        }

        return parsed;
    }

    /// <summary>
    /// Valkey に保存するセッショントークン情報
    /// </summary>
    private class SessionTokenInfo
    {
        public string UserId { get; init; } = string.Empty;

        public string MatchId { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; init; }
    }
}
