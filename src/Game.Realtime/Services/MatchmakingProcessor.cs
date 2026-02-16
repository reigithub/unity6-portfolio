using System.Text.Json;
using Game.Library.Shared.Realtime.Hubs;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// バックグラウンドマッチメイキングプロセッサ
/// キューを定期的にチェックし、マッチ成立時に Redis Pub/Sub で各プレイヤーに通知する。
/// </summary>
public class MatchmakingProcessor : BackgroundService
{
    private static readonly TimeSpan ProcessingInterval = TimeSpan.FromSeconds(2);

    private readonly IMatchmakingQueueService _queueService;
    private readonly IMatchSessionTokenService _tokenService;
    private readonly IConnectionMultiplexer _redis;
    private readonly MatchmakingConfiguration _config;
    private readonly ILogger<MatchmakingProcessor> _logger;

    public MatchmakingProcessor(
        IMatchmakingQueueService queueService,
        IMatchSessionTokenService tokenService,
        IConnectionMultiplexer redis,
        IOptions<MatchmakingConfiguration> config,
        ILogger<MatchmakingProcessor> logger)
    {
        _queueService = queueService;
        _tokenService = tokenService;
        _redis = redis;
        _config = config.Value;
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

            await Task.Delay(ProcessingInterval, stoppingToken);
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

    private async Task ProcessGameModeAsync(string gameMode, int matchSize, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var queueCount = await _queueService.GetQueueCountAsync(gameMode);
            if (queueCount < matchSize) break;

            var playerIds = await _queueService.DequeueTopPlayersAsync(gameMode, matchSize);
            if (playerIds.Length < matchSize)
            {
                _logger.LogWarning(
                    "Dequeued only {Count}/{MatchSize} players for mode {GameMode}, re-enqueuing",
                    playerIds.Length, matchSize, gameMode);

                // 足りない場合は再エンキュー
                foreach (var playerId in playerIds)
                {
                    await _queueService.EnqueuePlayerAsync(playerId, gameMode);
                }
                break;
            }

            await CreateMatchAsync(gameMode, playerIds);
        }
    }

    private async Task CreateMatchAsync(string gameMode, string[] playerIds)
    {
        var matchId = Guid.NewGuid().ToString("N");

        // 各プレイヤーにセッショントークン発行
        foreach (var playerId in playerIds)
        {
            await _tokenService.IssueTokenAsync(playerId, matchId);
        }

        var matchResult = new MatchResult
        {
            MatchId = matchId,
            PlayerIds = playerIds,
            ServerAddress = "pending",
            ServerPort = 0,
        };

        var json = JsonSerializer.Serialize(matchResult);
        var subscriber = _redis.GetSubscriber();

        // Per-user チャネルで通知（マッチしたプレイヤーのみ）
        foreach (var playerId in playerIds)
        {
            var channel = RedisChannel.Literal($"matchmaking:notify:{playerId}");
            await subscriber.PublishAsync(channel, json);
        }

        _logger.LogInformation(
            "Match {MatchId} created for mode {GameMode} with {PlayerCount} players: [{PlayerIds}]",
            matchId, gameMode, playerIds.Length, string.Join(", ", playerIds));
    }
}
