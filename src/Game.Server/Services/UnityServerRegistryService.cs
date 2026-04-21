using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Game.Server.Shared.Valkey;
using StackExchange.Redis;

namespace Game.Server.Services;

/// <summary>
/// Dedicated Server レジストリ管理サービス実装。
/// Valkey Hash <c>ds:registry</c> で DS 一覧を管理し、
/// Valkey String <c>ds:heartbeat:{dsId}</c>（TTL 60秒）でハートビートを管理する。
/// </summary>
public class UnityServerRegistryService : IUnityServerRegistryService
{
    /// <summary>Valkey Hash キー: DS 一覧（field=dsId, value=JSON）</summary>
    private const string RegistryKey = "ds:registry";

    /// <summary>Valkey String キープレフィックス: ハートビート TTL</summary>
    private const string HeartbeatKeyPrefix = "ds:heartbeat:";

    /// <summary>ハートビートの TTL（DS は 30秒間隔で送信）</summary>
    private static readonly TimeSpan HeartbeatTtl = TimeSpan.FromSeconds(60);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<UnityServerRegistryService> _logger;

    public UnityServerRegistryService(IConnectionMultiplexer redis, ILogger<UnityServerRegistryService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase GetDatabase() => _redis.GetDatabase();

    /// <summary>
    /// Dedicated Server をレジストリに登録する。
    /// </summary>
    /// <param name="request">DS の識別子・アドレス・ポート情報。</param>
    public Task RegisterAsync(UnityServerRegistrationRequest request)
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                var info = new DsInfo
                {
                    DsId = request.DsId,
                    Address = request.Address,
                    InternalAddress = request.InternalAddress ?? string.Empty,
                    GamePort = request.GamePort,
                    HealthPort = request.HealthPort,
                    Status = "idle",
                    CurrentSessionName = string.Empty,
                    RegisteredAt = DateTimeOffset.UtcNow,
                };

                var json = JsonHelper.Serialize(info);

                var batch = db.CreateBatch();
                var tasks = new[]
                {
                    batch.HashSetAsync(RegistryKey, request.DsId, json),
                    batch.StringSetAsync($"{HeartbeatKeyPrefix}{request.DsId}", "1", HeartbeatTtl),
                };
                batch.Execute();
                await Task.WhenAll(tasks);

                _logger.LogInformation(
                    "DS registered in Valkey: dsId={DsId}, address={Address}:{GamePort}, internalAddress={InternalAddress}",
                    request.DsId, request.Address, request.GamePort,
                    string.IsNullOrEmpty(info.InternalAddress) ? "(none)" : info.InternalAddress);
            },
            _logger,
            nameof(RegisterAsync));
    }

    /// <summary>
    /// Dedicated Server をレジストリから削除する。
    /// </summary>
    /// <param name="dsId">削除する DS の識別子。</param>
    public Task DeregisterAsync(string dsId)
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                var batch = db.CreateBatch();
                var tasks = new[]
                {
                    batch.HashDeleteAsync(RegistryKey, dsId),
                    batch.KeyDeleteAsync($"{HeartbeatKeyPrefix}{dsId}"),
                };
                batch.Execute();
                await Task.WhenAll(tasks);

                _logger.LogInformation("DS deregistered from Valkey: dsId={DsId}", dsId);
            },
            _logger,
            nameof(DeregisterAsync));
    }

    /// <summary>
    /// Dedicated Server のハートビートを更新する（TTL 60秒）。
    /// </summary>
    /// <param name="dsId">ハートビートを更新する DS の識別子。</param>
    public Task HeartbeatAsync(string dsId)
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                await db.StringSetAsync($"{HeartbeatKeyPrefix}{dsId}", "1", HeartbeatTtl);
                _logger.LogDebug("DS heartbeat updated: dsId={DsId}", dsId);
            },
            _logger,
            nameof(HeartbeatAsync));
    }

    /// <summary>
    /// アイドル状態（空き）の DS 一覧を返す。
    /// ハートビートが期限切れの DS は自動的に ds:registry から削除される。
    /// </summary>
    /// <returns>利用可能な DS 情報の配列。</returns>
    public Task<DsInfo[]> GetAvailableServersAsync()
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                var allEntries = await db.HashGetAllAsync(RegistryKey);
                if (allEntries.Length == 0)
                    return Array.Empty<DsInfo>();

                var available = new List<DsInfo>();
                var deadDsIds = new List<string>();

                foreach (var entry in allEntries)
                {
                    var dsId = entry.Name.ToString();
                    if (string.IsNullOrEmpty(dsId)) continue;

                    // ハートビートの存在確認（TTL 切れ = DS 死亡）
                    var heartbeatExists = await db.KeyExistsAsync($"{HeartbeatKeyPrefix}{dsId}");
                    if (!heartbeatExists)
                    {
                        deadDsIds.Add(dsId);
                        _logger.LogWarning("DS heartbeat expired, removing from registry: dsId={DsId}", dsId);
                        continue;
                    }

                    var info = JsonHelper.TryDeserialize<DsInfo>(entry.Value!, _logger, $"ds:registry field={dsId}");
                    if (info == null) continue;

                    if (info.Status == "idle")
                        available.Add(info);
                }

                // 死亡した DS をレジストリから削除
                if (deadDsIds.Count > 0)
                {
                    var deleteTasks = deadDsIds.Select(id => db.HashDeleteAsync(RegistryKey, id));
                    await Task.WhenAll(deleteTasks);
                }

                return available.ToArray();
            },
            fallback: Array.Empty<DsInfo>(),
            _logger,
            nameof(GetAvailableServersAsync));
    }

    /// <summary>
    /// DS のステータスを更新する。
    /// </summary>
    /// <param name="dsId">対象の DS 識別子。</param>
    /// <param name="status">"idle" または "active"。</param>
    /// <param name="sessionName">アクティブセッションの Fusion セッション名（SessionName）。idle 時は null。</param>
    public Task SetStatusAsync(string dsId, string status, string sessionName = null)
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                var raw = await db.HashGetAsync(RegistryKey, dsId);
                if (raw.IsNullOrEmpty)
                {
                    _logger.LogWarning("DS not found in registry for status update: dsId={DsId}", dsId);
                    return;
                }

                var info = JsonHelper.TryDeserialize<DsInfo>(raw!, _logger, $"ds:registry field={dsId}");
                if (info == null) return;

                info.Status = status;
                info.CurrentSessionName = sessionName ?? string.Empty;

                var json = JsonHelper.Serialize(info);
                await db.HashSetAsync(RegistryKey, dsId, json);

                _logger.LogInformation(
                    "DS status updated: dsId={DsId}, status={Status}, sessionName={SessionName}",
                    dsId, status, sessionName ?? "(none)");
            },
            _logger,
            nameof(SetStatusAsync));
    }

    /// <summary>
    /// DS のセッション終了を受け取り、ステータスを idle に戻す。
    /// </summary>
    /// <param name="dsId">セッションが終了した DS の識別子。</param>
    /// <param name="sessionName">終了した Fusion セッション名（SessionName）。</param>
    public Task SessionEndedAsync(string dsId, string sessionName)
    {
        return ValkeyExecutor.ExecuteAsync(
            async () =>
            {
                var db = GetDatabase();
                var raw = await db.HashGetAsync(RegistryKey, dsId);
                if (raw.IsNullOrEmpty)
                {
                    _logger.LogWarning(
                        "DS not found in registry for session-ended: dsId={DsId}, sessionName={SessionName}",
                        dsId, sessionName);
                    return;
                }

                var info = JsonHelper.TryDeserialize<DsInfo>(raw!, _logger, $"ds:registry field={dsId}");
                if (info == null) return;

                info.Status = "idle";
                info.CurrentSessionName = string.Empty;

                var json = JsonHelper.Serialize(info);
                await db.HashSetAsync(RegistryKey, dsId, json);

                _logger.LogInformation(
                    "DS session ended, status reset to idle: dsId={DsId}, sessionName={SessionName}",
                    dsId, sessionName);
            },
            _logger,
            nameof(SessionEndedAsync));
    }
}
