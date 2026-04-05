using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

/// <summary>
/// Unity Dedicated Server 接続トークン発行エンドポイント。
/// SP/MP 共通で認証済みユーザーに HMAC 署名付きセッショントークンを発行する。
/// </summary>
[ApiController]
[Route("api/unity-server-auth")]
[Authorize]
public class UnityServerAuthController : ControllerBase
{
    private readonly IUnityServerAuthService _authService;
    private readonly ILogger<UnityServerAuthController> _logger;

    public UnityServerAuthController(
        IUnityServerAuthService authService,
        ILogger<UnityServerAuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 認証済みユーザーに Unity Dedicated Server 接続用トークンを発行する。
    /// クライアントはこのトークンを Fusion ConnectionToken に設定して接続する。
    /// </summary>
    /// <returns>セッショントークンとセッション名を含むレスポンス。</returns>
    [HttpPost("issue-token")]
    [ProducesResponseType(typeof(UnityServerAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueToken([FromQuery] string matchId = null)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var response = await _authService.IssueTokenAsync(userId, matchId);

        _logger.LogInformation(
            "Unity server auth token issued for user {UserId}, session {SessionName}",
            userId, response.SessionName);

        return Ok(response);
    }
}
