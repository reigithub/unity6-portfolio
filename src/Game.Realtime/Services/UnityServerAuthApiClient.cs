using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Game.Library.Shared.Dto;
using Game.Server.Shared.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Game.Realtime.Services;

/// <summary>
/// Game.Server の Unity Server 認証 API クライアントインターフェース。
/// マッチ成立時にサーバー間通信でトークンを取得する。
/// </summary>
public interface IUnityServerAuthApiClient
{
    /// <summary>
    /// Game.Server の <c>POST /api/unity-server-auth/issue-token</c> を呼び出し、
    /// Unity Dedicated Server 接続用トークンを取得する。
    /// </summary>
    /// <param name="userId">トークン発行対象のユーザーID。</param>
    /// <param name="matchId">マッチID。null の場合はサーバーが自動生成（SP 用）。</param>
    /// <returns>セッショントークンとセッション名を含むレスポンス。</returns>
    Task<UnityServerAuthResponse> IssueTokenAsync(string userId, string matchId = null);
}

/// <summary>
/// Game.Server へのサービス間 HTTP クライアント実装。
/// 共有 JWT シークレットでサービストークンを生成し、Authorization ヘッダーに設定して呼び出す。
/// </summary>
public class UnityServerAuthApiClient : IUnityServerAuthApiClient
{
    /// <summary>
    /// HttpClient の名前（IHttpClientFactory 用）
    /// </summary>
    public const string HttpClientName = "GameServer";
    private const string IssueTokenEndpoint = "/api/unity-server-auth/issue-token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GameServerSettings _gameServerSettings;
    private readonly JwtValidationSettings _jwtSettings;
    private readonly ILogger<UnityServerAuthApiClient> _logger;

    public UnityServerAuthApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GameServerSettings> gameServerSettings,
        IOptions<JwtValidationSettings> jwtSettings,
        ILogger<UnityServerAuthApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gameServerSettings = gameServerSettings.Value;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UnityServerAuthResponse> IssueTokenAsync(string userId, string matchId = null)
    {
        var serviceToken = CreateServiceToken(userId);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(_gameServerSettings.BaseUrl);

        var endpoint = string.IsNullOrEmpty(matchId)
            ? IssueTokenEndpoint
            : $"{IssueTokenEndpoint}?matchId={Uri.EscapeDataString(matchId)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogDebug("Game.Server にトークン発行リクエスト送信: userId={UserId}", userId);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UnityServerAuthResponse>()
            ?? throw new InvalidOperationException($"Game.Server からの応答を UnityServerAuthResponse にデシリアライズできません (userId={userId})");

        _logger.LogInformation(
            "Game.Server からトークン取得成功: userId={UserId}, sessionName={SessionName}",
            userId,
            result.SessionName);

        return result;
    }

    /// <summary>
    /// 共有 JWT シークレットで userId を claims に含むサービストークンを生成する。
    /// Game.Server の <c>[Authorize]</c> エンドポイントが検証に使用する。
    /// </summary>
    /// <param name="userId">sub クレームに設定するユーザーID。</param>
    /// <returns>署名済み JWT トークン文字列。</returns>
    private string CreateServiceToken(string userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
