using Game.Library.Shared.Dto;
using Game.Server.Configuration;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Game.Server.Controllers;

/// <summary>
/// Unity Dedicated Server 管理エンドポイント。
/// トークン発行（JWT 認証）と DS ライフサイクル管理（共有シークレット認証）を統合する。
/// </summary>
[ApiController]
[Route("api/unity-server")]
public class UnityServerController : ControllerBase
{
    private readonly IUnityServerService _serverService;
    private readonly IUnityServerRegistryService _registryService;
    private readonly UnityServerSettings _settings;
    private readonly ILogger<UnityServerController> _logger;

    public UnityServerController(
        IUnityServerService serverService,
        IUnityServerRegistryService registryService,
        IOptions<UnityServerSettings> settings,
        ILogger<UnityServerController> logger)
    {
        _serverService = serverService;
        _registryService = registryService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// 認証済みユーザーに Unity Dedicated Server 接続用トークンを発行する。
    /// クライアントはこのトークンを Fusion ConnectionToken に設定して接続する。
    /// stageId が 0 より大きい場合は DS へのセッション割り当ても実行する。
    /// </summary>
    /// <param name="matchId">マッチID。null の場合はサーバーが自動生成（SP 用）。</param>
    /// <param name="stageId">ステージID。0 の場合は DS 割り当てをスキップ（SP 用）。</param>
    /// <param name="expectedPlayers">期待プレイヤー数。DS 割り当て時に渡す（デフォルト: 1）。</param>
    /// <returns>セッショントークンとセッション名を含むレスポンス。</returns>
    [HttpPost("issue-token")]
    [Authorize]
    [ProducesResponseType(typeof(UnityServerAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueToken(
        [FromQuery] string matchId = null,
        [FromQuery] int stageId = 0,
        [FromQuery] int expectedPlayers = 1)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var response = await _serverService.IssueTokenAsync(userId, matchId, stageId, expectedPlayers);

        _logger.LogInformation(
            "Unity server token issued for user {UserId}, session {SessionName}, stageId={StageId}",
            userId, response.SessionName, stageId);

        return Ok(response);
    }

    /// <summary>
    /// Dedicated Server を DS レジストリに登録する。
    /// DS 起動時に呼ばれる。
    /// </summary>
    /// <param name="request">DS の識別子・アドレス・ポート情報。</param>
    /// <returns>登録成功時は 200 OK。</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] UnityServerRegistrationRequest request)
    {
        if (!ValidateDsAuth())
            return Unauthorized();

        await _registryService.RegisterAsync(request);

        _logger.LogInformation("DS registered: dsId={DsId}, address={Address}, gamePort={GamePort}", request.DsId, request.Address, request.GamePort);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server を DS レジストリから登録解除する。
    /// DS 正常終了時に呼ばれる。
    /// </summary>
    /// <param name="dsId">登録解除する DS の識別子。</param>
    /// <returns>登録解除成功時は 200 OK。</returns>
    [HttpPost("deregister")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Deregister([FromQuery] string dsId)
    {
        if (!ValidateDsAuth())
            return Unauthorized();

        await _registryService.DeregisterAsync(dsId);

        _logger.LogInformation("DS deregistered: dsId={DsId}", dsId);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server のハートビートを受信し、生存確認を更新する。
    /// DS は 30 秒間隔でこのエンドポイントを呼ぶ。
    /// </summary>
    /// <param name="dsId">ハートビートを送信する DS の識別子。</param>
    /// <returns>ハートビート受信成功時は 200 OK。</returns>
    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Heartbeat([FromQuery] string dsId)
    {
        if (!ValidateDsAuth())
            return Unauthorized();

        await _registryService.HeartbeatAsync(dsId);

        _logger.LogDebug("DS heartbeat received: dsId={DsId}", dsId);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server からセッション終了を通知する。
    /// DS がセッション完了後に呼び出し、DS ステータスを idle に戻す。
    /// </summary>
    /// <param name="dsId">セッションが終了した DS の識別子。</param>
    /// <param name="matchId">終了したセッションのマッチID。</param>
    /// <returns>セッション終了通知受信成功時は 200 OK。</returns>
    [HttpPost("session-ended")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SessionEnded([FromQuery] string dsId, [FromQuery] string matchId)
    {
        if (!ValidateDsAuth())
            return Unauthorized();

        await _registryService.SessionEndedAsync(dsId, matchId);

        _logger.LogInformation(
            "DS session ended: dsId={DsId}, matchId={MatchId}", dsId, matchId);

        return Ok();
    }

    /// <summary>
    /// リクエストヘッダーの共有シークレットを検証する。
    /// DS から Game.Server へのリクエストに使用する。
    /// </summary>
    /// <returns>認証成功時は true、失敗時は false。</returns>
    private bool ValidateDsAuth()
    {
        if (!Request.Headers.TryGetValue("X-DS-Auth", out var authHeader))
        {
            _logger.LogWarning("DS auth header missing: {Path}", Request.Path);
            return false;
        }

        if (authHeader.ToString() != _settings.SecretKey)
        {
            _logger.LogWarning("DS auth secret mismatch: {Path}", Request.Path);
            return false;
        }

        return true;
    }
}
