using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Grpc.Core;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Hubs;

/// <summary>
/// マッチメイキングHub サーバー実装
/// </summary>
public class MatchmakingHub : StreamingHubBase<IMatchmakingHub, IMatchmakingHubReceiver>, IMatchmakingHub
{
    private readonly ILogger<MatchmakingHub> _logger;
    private readonly IMatchmakingService _matchmakingService;

    private IGroup<IMatchmakingHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _gameMode = string.Empty;

    public MatchmakingHub(
        ILogger<MatchmakingHub> logger,
        IMatchmakingService matchmakingService)
    {
        _logger = logger;
        _matchmakingService = matchmakingService;
    }

    public async ValueTask StartMatchmakingAsync(string gameMode)
    {
        _userId = Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value
            ?? ConnectionId.ToString();
        _gameMode = gameMode;

        var queueGroupName = $"matchmaking:{gameMode}";
        _currentGroup = await Group.AddAsync(queueGroupName);

        await _matchmakingService.EnqueuePlayerAsync(_userId, gameMode);

        var queueCount = await _matchmakingService.GetQueueCountAsync(gameMode);
        var estimatedWait = Math.Max(10, 60 / Math.Max(1, queueCount));

        _logger.LogInformation(
            "Player {UserId} started matchmaking for mode {GameMode}. Queue: {QueueCount}",
            _userId,
            gameMode,
            queueCount);

        Client.OnMatchmakingStarted(estimatedWait);
        _currentGroup.All.OnQueueStatusUpdated(queueCount);
    }

    public async ValueTask CancelMatchmakingAsync()
    {
        if (!string.IsNullOrEmpty(_gameMode))
        {
            await _matchmakingService.DequeuePlayerAsync(_userId, _gameMode);

            _logger.LogInformation(
                "Player {UserId} cancelled matchmaking for mode {GameMode}",
                _userId,
                _gameMode);

            Client.OnMatchmakingCancelled("Cancelled by player");

            if (_currentGroup != null)
            {
                var queueCount = await _matchmakingService.GetQueueCountAsync(_gameMode);
                _currentGroup.All.OnQueueStatusUpdated(queueCount);
                await _currentGroup.RemoveAsync(Context);
                _currentGroup = null;
            }
        }
    }

    public async ValueTask<int> GetQueueCountAsync(string gameMode)
    {
        return await _matchmakingService.GetQueueCountAsync(gameMode);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (!string.IsNullOrEmpty(_gameMode))
        {
            await _matchmakingService.DequeuePlayerAsync(_userId, _gameMode);
        }

        if (_currentGroup != null)
        {
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        _logger.LogInformation("Player {UserId} disconnected from matchmaking", _userId);
    }
}
