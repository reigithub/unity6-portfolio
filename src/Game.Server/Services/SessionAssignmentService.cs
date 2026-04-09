using System.Text;
using Game.Server.Configuration;
using Game.Server.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Game.Server.Services;

/// <summary>
/// Dedicated Server へのセッション割り当てサービス実装。
/// 空き DS を選択し、HTTP POST /session/start でセッション作成を指示する。
/// </summary>
public class SessionAssignmentService : ISessionAssignmentService
{
    private const string SessionStartPath = "/session/start";

    private readonly IUnityServerRegistryService _registryService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UnityServerSettings _settings;
    private readonly ILogger<SessionAssignmentService> _logger;

    public SessionAssignmentService(
        IUnityServerRegistryService registryService,
        IHttpClientFactory httpClientFactory,
        IOptions<UnityServerSettings> settings,
        ILogger<SessionAssignmentService> logger)
    {
        _registryService = registryService;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// 空き DS を選択し、セッション作成を指示する。
    /// DS に POST /session/start を送信し、ステータスを active に更新する。
    /// </summary>
    /// <param name="matchId">割り当てるマッチID。</param>
    /// <param name="stageId">ステージID。</param>
    /// <param name="expectedPlayers">期待プレイヤー数。</param>
    /// <exception cref="InvalidOperationException">空き DS が存在しない場合。</exception>
    public async Task AssignSessionAsync(string matchId, int stageId, int expectedPlayers)
    {
        // 1. DS 一覧取得（ハートビート確認済み + 死亡 DS 自動削除）
        var servers = await _registryService.GetAvailableServersAsync();

        // GetAvailableServersAsync は idle DS のみを返す
        if (servers.Length == 0)
        {
            _logger.LogWarning(
                "空き DS が存在しないためセッション割り当て不可: matchId={MatchId}", matchId);
            throw new InvalidOperationException("No available dedicated servers");
        }

        // 2. 最初の idle DS を選択
        var target = servers[0];

        _logger.LogInformation(
            "DS を選択: dsId={DsId}, address={Address}:{HealthPort}, matchId={MatchId}",
            target.DsId, target.Address, target.HealthPort, matchId);

        // 3. DS に HTTP POST でセッション作成指示
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var requestBody = $"{{\"matchId\":\"{matchId}\",\"stageId\":{stageId},\"expectedPlayers\":{expectedPlayers}}}";
        var url = $"http://{target.Address}:{target.HealthPort}{SessionStartPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        // 共有シークレット認証
        if (!string.IsNullOrEmpty(_settings.SecretKey))
            request.Headers.Add("X-DS-Auth", _settings.SecretKey);

        _logger.LogDebug(
            "DS へセッション開始リクエスト送信: url={Url}, matchId={MatchId}, stageId={StageId}, expectedPlayers={ExpectedPlayers}",
            url, matchId, stageId, expectedPlayers);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // 4. DS ステータスを active に更新
        await _registryService.SetStatusAsync(target.DsId, "active", matchId);

        _logger.LogInformation(
            "セッション割り当て完了: dsId={DsId}, address={Address}:{HealthPort}, matchId={MatchId}",
            target.DsId, target.Address, target.HealthPort, matchId);
    }
}
