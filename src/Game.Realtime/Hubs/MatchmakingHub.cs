using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Validation;
using Game.Server.Shared.Extensions;
using Grpc.Core;
using MagicOnion.Server.Hubs;
using StackExchange.Redis;

namespace Game.Realtime.Hubs;

/// <summary>
/// マッチメイキングHub サーバー実装（通知専用）
/// キュー操作は Unary IMatchmakingService 経由。Hub は Redis Pub/Sub でマッチ成立通知を受信し、クライアントに転送する。
/// </summary>
public class MatchmakingHub : StreamingHubBase<IMatchmakingHub, IMatchmakingHubReceiver>, IMatchmakingHub
{
    private readonly ILogger<MatchmakingHub> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMatchmakingValidator _matchmakingValidator;

    private IGroup<IMatchmakingHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _gameMode = string.Empty;
    private ISubscriber? _subscriber;

    public MatchmakingHub(
        ILogger<MatchmakingHub> logger,
        IConnectionMultiplexer redis,
        IMatchmakingValidator matchmakingValidator)
    {
        _logger = logger;
        _redis = redis;
        _matchmakingValidator = matchmakingValidator;
    }

    public async ValueTask SubscribeAsync(string gameMode)
    {
        _matchmakingValidator.ValidateGameMode(gameMode);

        _userId = Context.CallContext.GetHttpContext().User.GetRequiredUserId();
        _gameMode = gameMode;

        var queueGroupName = $"matchmaking:{gameMode}";
        _currentGroup = await Group.AddAsync(queueGroupName);

        // Redis Pub/Sub でマッチ成立通知を購読
        _subscriber = _redis.GetSubscriber();
        var channel = RedisChannel.Literal($"matchmaking:notify:{_userId}");
        await _subscriber.SubscribeAsync(channel, (_, message) =>
        {
            var result = JsonHelper.TryDeserialize<MatchResult>(message.ToString(), _logger, $"match result for user {_userId}");
            if (result != null)
            {
                try
                {
                    Client.OnMatchFound(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send match notification to user {UserId}", _userId);
                }
            }
        });

        // キューステータス更新の購読
        var queueChannel = RedisChannel.Literal($"matchmaking:queue:{gameMode}");
        await _subscriber.SubscribeAsync(queueChannel, (_, message) =>
        {
            try
            {
                if (int.TryParse(message.ToString(), out var count))
                {
                    Client.OnQueueStatusUpdated(count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send queue status update to user {UserId}", _userId);
            }
        });

        _logger.LogInformation(
            "Player {UserId} subscribed to matchmaking notifications for mode {GameMode}",
            _userId, gameMode);

        Client.OnMatchmakingStarted(30);
    }

    public async ValueTask UnsubscribeAsync()
    {
        await UnsubscribeRedisAsync();

        if (_currentGroup != null)
        {
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        _logger.LogInformation(
            "Player {UserId} unsubscribed from matchmaking notifications",
            _userId);

        Client.OnMatchmakingCancelled("Unsubscribed by player");
    }

    protected override async ValueTask OnDisconnected()
    {
        await UnsubscribeRedisAsync();

        if (_currentGroup != null)
        {
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        _logger.LogInformation("Player {UserId} disconnected from matchmaking hub", _userId);
    }

    private async Task UnsubscribeRedisAsync()
    {
        if (_subscriber != null && !string.IsNullOrEmpty(_userId))
        {
            var notifyChannel = RedisChannel.Literal($"matchmaking:notify:{_userId}");
            await _subscriber.UnsubscribeAsync(notifyChannel);

            if (!string.IsNullOrEmpty(_gameMode))
            {
                var queueChannel = RedisChannel.Literal($"matchmaking:queue:{_gameMode}");
                await _subscriber.UnsubscribeAsync(queueChannel);
            }

            _subscriber = null;
        }
    }
}
