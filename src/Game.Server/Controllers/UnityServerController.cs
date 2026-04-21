using Game.Library.Shared.Dto;
using Game.Server.Attributes;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

/// <summary>
/// Unity Dedicated Server 管理エンドポイント。
/// トークン発行（JWT 認証）と DS ライフサイクル管理（HMAC 署名認証）を統合する。
/// DS ライフサイクル系 Action の認証は <see cref="Middleware.RequestSigningMiddleware"/> で行われる。
/// </summary>
[ApiController]
[Route("api/unity-server")]
public class UnityServerController : ControllerBase
{
    private readonly IUnityServerAuthService _serverAuthService;
    private readonly IUnityServerRegistryService _registryService;
    private readonly ILogger<UnityServerController> _logger;

    public UnityServerController(
        IUnityServerAuthService serverAuthService,
        IUnityServerRegistryService registryService,
        ILogger<UnityServerController> logger)
    {
        _serverAuthService = serverAuthService;
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// 認証済みユーザーに Unity Dedicated Server 接続用トークンを発行する。
    /// クライアントはこのトークンを Fusion ConnectionToken に設定して接続する。
    /// stageId が 0 より大きい場合は DS へのセッション割り当ても実行する。
    /// </summary>
    /// <param name="sessionName">Fusion セッション名（SessionName）。null の場合はサーバーが自動生成（SP 用）。</param>
    /// <param name="stageId">ステージID。0 の場合は DS 割り当てをスキップ（SP 用）。</param>
    /// <param name="playerCount">プレイヤー数。DS 割り当て時に渡す（デフォルト: 1）。</param>
    /// <returns>セッショントークンとセッション名を含むレスポンス。</returns>
    [HttpPost("issue-token")]
    [Authorize]
    [SkipRequestSigning]
    [ProducesResponseType(typeof(UnityServerAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueToken(
        [FromQuery] string sessionName = null,
        [FromQuery] int stageId = 0,
        [FromQuery] int playerCount = 1)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var response = await _serverAuthService.IssueTokenAsync(userId, sessionName, stageId, playerCount);

        _logger.LogInformation(
            "Unity server token issued for user {UserId}, session {SessionName}, stageId={StageId}",
            userId, response.SessionName, stageId);

        return Ok(response);
    }

    /// <summary>
    /// Dedicated Server を DS レジストリに登録する。
    /// DS 起動時に呼ばれる。認証は RequestSigningMiddleware で処理済み。
    /// </summary>
    /// <param name="request">DS の識別子・アドレス・ポート情報。</param>
    /// <returns>登録成功時は 200 OK。</returns>
    [HttpPost("register")]
    [UnityServerSignature]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] UnityServerRegistrationRequest request)
    {
        await _registryService.RegisterAsync(request);

        _logger.LogInformation(
            "DS registered: dsId={DsId}, address={Address}, gamePort={GamePort}, internalAddress={InternalAddress}",
            request.DsId, request.Address, request.GamePort,
            string.IsNullOrEmpty(request.InternalAddress) ? "(none)" : request.InternalAddress);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server を DS レジストリから登録解除する。
    /// DS 正常終了時に呼ばれる。認証は RequestSigningMiddleware で処理済み。
    /// </summary>
    /// <param name="request">登録解除する DS の識別子を含むリクエスト。</param>
    /// <returns>登録解除成功時は 200 OK。</returns>
    [HttpPost("deregister")]
    [UnityServerSignature]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Deregister([FromBody] UnityServerDeregisterRequest request)
    {
        await _registryService.DeregisterAsync(request.DsId);

        _logger.LogInformation("DS deregistered: dsId={DsId}", request.DsId);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server のハートビートを受信し、生存確認を更新する。
    /// DS は 30 秒間隔でこのエンドポイントを呼ぶ。認証は RequestSigningMiddleware で処理済み。
    /// </summary>
    /// <param name="request">ハートビートを送信する DS の識別子を含むリクエスト。</param>
    /// <returns>ハートビート受信成功時は 200 OK。</returns>
    [HttpPost("heartbeat")]
    [UnityServerSignature]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Heartbeat([FromBody] UnityServerHeartbeatRequest request)
    {
        await _registryService.HeartbeatAsync(request.DsId);

        _logger.LogDebug("DS heartbeat received: dsId={DsId}", request.DsId);

        return Ok();
    }

    /// <summary>
    /// Dedicated Server からセッション終了を通知する。
    /// DS がセッション完了後に呼び出し、DS ステータスを idle に戻す。
    /// 認証は RequestSigningMiddleware で処理済み。
    /// </summary>
    /// <param name="request">セッションが終了した DS の識別子と Fusion セッション名を含むリクエスト。</param>
    /// <returns>セッション終了通知受信成功時は 200 OK。</returns>
    [HttpPost("session-ended")]
    [UnityServerSignature]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SessionEnded([FromBody] UnityServerSessionEndedRequest request)
    {
        await _registryService.SessionEndedAsync(request.DsId, request.SessionName);

        _logger.LogInformation(
            "DS session ended: dsId={DsId}, sessionName={SessionName}", request.DsId, request.SessionName);

        return Ok();
    }
}
