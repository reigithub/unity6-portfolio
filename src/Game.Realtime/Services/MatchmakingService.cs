using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Services;
using Game.Realtime.Extensions;
using Game.Realtime.Validation;
using MagicOnion;
using MagicOnion.Server;

namespace Game.Realtime.Services;

/// <summary>
/// マッチメイキング Unary RPC サービス実装
/// </summary>
public class MatchmakingService : ServiceBase<IMatchmakingService>, IMatchmakingService
{
    private readonly IMatchmakingQueueService _queueService;
    private readonly IMatchmakingValidator _matchmakingValidator;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(
        IMatchmakingQueueService queueService,
        IMatchmakingValidator matchmakingValidator,
        ILogger<MatchmakingService> logger)
    {
        _queueService = queueService;
        _matchmakingValidator = matchmakingValidator;
        _logger = logger;
    }

    public async UnaryResult<MatchmakingResponse> EnqueueAsync(MatchmakingRequest request)
    {
        var userId = Context.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return new MatchmakingResponse
            {
                Success = false,
                ErrorMessage = "User not authenticated",
            };
        }

        _matchmakingValidator.ValidateGameMode(request.GameMode);

        try
        {
            await _queueService.EnqueuePlayerAsync(userId, request.GameMode);
            var queueCount = await _queueService.GetQueueCountAsync(request.GameMode);
            var estimatedWait = Math.Max(10, 60 / Math.Max(1, queueCount));

            _logger.LogInformation(
                "Player {UserId} enqueued for mode {GameMode}. Queue: {QueueCount}",
                userId, request.GameMode, queueCount);

            return new MatchmakingResponse
            {
                Success = true,
                TicketId = $"{userId}:{request.GameMode}",
                EstimatedWaitSeconds = estimatedWait,
                PlayersInQueue = queueCount,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue player {UserId}", userId);
            return new MatchmakingResponse
            {
                Success = false,
                ErrorMessage = "Failed to join matchmaking queue",
            };
        }
    }

    public async UnaryResult<MatchmakingResponse> DequeueAsync(MatchmakingRequest request)
    {
        var userId = Context.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return new MatchmakingResponse
            {
                Success = false,
                ErrorMessage = "User not authenticated",
            };
        }

        _matchmakingValidator.ValidateGameMode(request.GameMode);

        try
        {
            await _queueService.DequeuePlayerAsync(userId, request.GameMode);
            var queueCount = await _queueService.GetQueueCountAsync(request.GameMode);

            _logger.LogInformation(
                "Player {UserId} dequeued from mode {GameMode}",
                userId, request.GameMode);

            return new MatchmakingResponse
            {
                Success = true,
                PlayersInQueue = queueCount,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue player {UserId}", userId);
            return new MatchmakingResponse
            {
                Success = false,
                ErrorMessage = "Failed to leave matchmaking queue",
            };
        }
    }

    public async UnaryResult<int> GetQueueCountAsync(string gameMode)
    {
        _matchmakingValidator.ValidateGameMode(gameMode);

        return await _queueService.GetQueueCountAsync(gameMode);
    }
}
