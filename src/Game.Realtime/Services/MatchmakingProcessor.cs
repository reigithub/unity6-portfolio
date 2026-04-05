using Game.Library.Shared.Realtime.Hubs;
using Game.Server.Shared.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// バックグラウンドマッチメイキングプロセッサ
/// stageId 別キュー + "any" キュー + matchSize ベースでマッチを形成する。
/// </summary>
public class MatchmakingProcessor : BackgroundService
{
    private readonly IMatchmakingQueueService _queueService;
    private readonly IUnityServerAuthApiClient _unityServerAuthApi;
    private readonly IConnectionMultiplexer _redis;
    private readonly MatchmakingConfiguration _config;
    private readonly UnityServerConfiguration _unityServerConfig;
    private readonly ILogger<MatchmakingProcessor> _logger;

    public MatchmakingProcessor(
        IMatchmakingQueueService queueService,
        IUnityServerAuthApiClient unityServerAuthApi,
        IConnectionMultiplexer redis,
        IOptions<MatchmakingConfiguration> config,
        IOptions<UnityServerConfiguration> unityServerConfig,
        ILogger<MatchmakingProcessor> logger)
    {
        _queueService = queueService;
        _unityServerAuthApi = unityServerAuthApi;
        _redis = redis;
        _config = config.Value;
        _unityServerConfig = unityServerConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MatchmakingProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllGameModesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in matchmaking processor loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.ProcessingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("MatchmakingProcessor stopped");
    }

    private async Task ProcessAllGameModesAsync(CancellationToken stoppingToken)
    {
        foreach (var (gameMode, config) in _config.GameModes)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await ProcessGameModeAsync(gameMode, config.MatchSize, stoppingToken);
        }
    }

    private async Task ProcessGameModeAsync(string gameMode, int maxMatchSize, CancellationToken stoppingToken)
    {
        var stageKeys = await _queueService.GetActiveStageKeysAsync(gameMode);
        if (stageKeys.Length == 0) return;

        // Phase 1: stageId 指定キュー（数値の stageKey）を処理
        foreach (var stageKey in stageKeys)
        {
            if (stageKey == "any") continue;
            if (!int.TryParse(stageKey, out var stageId)) continue;

            stoppingToken.ThrowIfCancellationRequested();
            await ProcessStageQueueAsync(gameMode, stageId, maxMatchSize, stoppingToken);
        }

        // Phase 2: "any" キューの残りを処理
        if (stageKeys.Contains("any"))
        {
            stoppingToken.ThrowIfCancellationRequested();
            await ProcessAnyQueueAsync(gameMode, maxMatchSize, stoppingToken);
        }
    }

    /// <summary>
    /// stageId 指定キューを処理。"any" キューからも同じ matchSize のプレイヤーを補充可能。
    /// </summary>
    private async Task ProcessStageQueueAsync(string gameMode, int stageId, int maxMatchSize, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var queueCount = await _queueService.GetQueueCountAsync(gameMode, stageId);
            if (queueCount == 0) break;

            // キュー先頭のプレイヤーの matchSize を基準にする
            var peeked = await _queueService.DequeueTopPlayersAsync(gameMode, stageId, 1);
            if (peeked.Length == 0) break;

            var leaderId = peeked[0];
            var matchSize = await _queueService.GetPlayerMatchSizeAsync(leaderId);
            matchSize = Math.Clamp(matchSize, 2, maxMatchSize);

            // stageId キューから同じ matchSize のプレイヤーを集める
            var candidates = new List<string> { leaderId };
            await CollectMatchingPlayersAsync(candidates, gameMode, stageId, matchSize, stoppingToken);

            // 足りない場合は "any" キューから補充
            if (candidates.Count < matchSize)
            {
                await CollectMatchingPlayersFromAnyAsync(candidates, gameMode, matchSize, stoppingToken);
            }

            if (candidates.Count >= matchSize)
            {
                var matchPlayers = candidates.GetRange(0, matchSize).ToArray();

                // 余剰プレイヤーがいれば stageId キューに戻す
                for (var i = matchSize; i < candidates.Count; i++)
                {
                    var ms = await _queueService.GetPlayerMatchSizeAsync(candidates[i]);
                    await _queueService.EnqueuePlayerAsync(candidates[i], gameMode, stageId, ms);
                }

                await CreateMatchAsync(gameMode, matchPlayers, stageId);
            }
            else
            {
                // マッチ不成立 → 全員戻す
                foreach (var playerId in candidates)
                {
                    var ms = await _queueService.GetPlayerMatchSizeAsync(playerId);
                    await _queueService.EnqueuePlayerAsync(playerId, gameMode, stageId, ms);
                }
                break;
            }
        }
    }

    /// <summary>
    /// "any" キューの残りプレイヤー同士でマッチ形成。stageId はサーバーが決定。
    /// </summary>
    private async Task ProcessAnyQueueAsync(string gameMode, int maxMatchSize, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var queueCount = await _queueService.GetQueueCountAsync(gameMode, 0);
            if (queueCount < 2) break;

            var peeked = await _queueService.DequeueTopPlayersAsync(gameMode, 0, 1);
            if (peeked.Length == 0) break;

            var leaderId = peeked[0];
            var matchSize = await _queueService.GetPlayerMatchSizeAsync(leaderId);
            matchSize = Math.Clamp(matchSize, 2, maxMatchSize);

            var candidates = new List<string> { leaderId };
            await CollectMatchingPlayersFromAnyAsync(candidates, gameMode, matchSize, stoppingToken);

            if (candidates.Count >= matchSize)
            {
                var matchPlayers = candidates.GetRange(0, matchSize).ToArray();

                for (var i = matchSize; i < candidates.Count; i++)
                {
                    var ms = await _queueService.GetPlayerMatchSizeAsync(candidates[i]);
                    await _queueService.EnqueuePlayerAsync(candidates[i], gameMode, 0, ms);
                }

                // stageId = 1 をデフォルトとして使用
                await CreateMatchAsync(gameMode, matchPlayers, 1);
            }
            else
            {
                foreach (var playerId in candidates)
                {
                    var ms = await _queueService.GetPlayerMatchSizeAsync(playerId);
                    await _queueService.EnqueuePlayerAsync(playerId, gameMode, 0, ms);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 指定 stageId キューから同じ matchSize のプレイヤーを収集
    /// </summary>
    private async Task CollectMatchingPlayersAsync(
        List<string> candidates, string gameMode, int stageId, int targetMatchSize, CancellationToken stoppingToken)
    {
        var needed = targetMatchSize - candidates.Count;
        if (needed <= 0) return;

        // バッチで取得して matchSize が一致するものだけ残す
        var batch = await _queueService.DequeueTopPlayersAsync(gameMode, stageId, needed * 2);
        var requeue = new List<(string userId, int matchSize)>();

        foreach (var playerId in batch)
        {
            stoppingToken.ThrowIfCancellationRequested();

            if (candidates.Count >= targetMatchSize)
            {
                var ms = await _queueService.GetPlayerMatchSizeAsync(playerId);
                requeue.Add((playerId, ms));
                continue;
            }

            var playerMatchSize = await _queueService.GetPlayerMatchSizeAsync(playerId);
            if (playerMatchSize == targetMatchSize)
            {
                candidates.Add(playerId);
            }
            else
            {
                requeue.Add((playerId, playerMatchSize));
            }
        }

        // 不一致プレイヤーを戻す
        foreach (var (userId, ms) in requeue)
        {
            await _queueService.EnqueuePlayerAsync(userId, gameMode, stageId, ms);
        }
    }

    /// <summary>
    /// "any" キューから同じ matchSize のプレイヤーを収集
    /// </summary>
    private async Task CollectMatchingPlayersFromAnyAsync(
        List<string> candidates, string gameMode, int targetMatchSize, CancellationToken stoppingToken)
    {
        var needed = targetMatchSize - candidates.Count;
        if (needed <= 0) return;

        var batch = await _queueService.DequeueTopPlayersAsync(gameMode, 0, needed * 2);
        var requeue = new List<(string userId, int matchSize)>();

        foreach (var playerId in batch)
        {
            stoppingToken.ThrowIfCancellationRequested();

            if (candidates.Count >= targetMatchSize)
            {
                var ms = await _queueService.GetPlayerMatchSizeAsync(playerId);
                requeue.Add((playerId, ms));
                continue;
            }

            var playerMatchSize = await _queueService.GetPlayerMatchSizeAsync(playerId);
            if (playerMatchSize == targetMatchSize)
            {
                candidates.Add(playerId);
            }
            else
            {
                requeue.Add((playerId, playerMatchSize));
            }
        }

        foreach (var (userId, ms) in requeue)
        {
            await _queueService.EnqueuePlayerAsync(userId, gameMode, 0, ms);
        }
    }

    private async Task CreateMatchAsync(string gameMode, string[] playerIds, int stageId)
    {
        var matchId = $"mp-{Guid.NewGuid():N}";
        var subscriber = _redis.GetSubscriber();

        // 全プレイヤーに同一 matchId でトークンを発行し、MatchResult を配信
        await Task.WhenAll(playerIds.Select(async playerId =>
        {
            var authResponse = await _unityServerAuthApi.IssueTokenAsync(playerId, matchId);

            var matchResult = new MatchResult
            {
                MatchId = matchId,
                PlayerIds = playerIds,
                ServerAddress = _unityServerConfig.ServerAddress,
                ServerPort = _unityServerConfig.ServerPort,
                SessionToken = authResponse.Token,
                StageId = stageId,
            };

            var json = JsonHelper.Serialize(matchResult);
            var channel = RedisChannel.Literal($"matchmaking:notify:{playerId}");
            await subscriber.PublishAsync(channel, json);

            await _queueService.CleanupPlayerAsync(playerId);
        }));

        _logger.LogInformation(
            "Match {MatchId} created for mode {GameMode}, stage {StageId} with {PlayerCount} players: [{PlayerIds}]",
            matchId, gameMode, stageId, playerIds.Length, string.Join(", ", playerIds));
    }
}
