using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Services;
using Game.Realtime.Extensions;
using MagicOnion;
using MagicOnion.Server;

namespace Game.Realtime.Services;

/// <summary>
/// ロビー Unary RPC サービス実装
/// </summary>
public class LobbyService : ServiceBase<ILobbyService>, ILobbyService
{
    private readonly ILobbyDataService _lobbyDataService;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(ILobbyDataService lobbyDataService, ILogger<LobbyService> logger)
    {
        _lobbyDataService = lobbyDataService;
        _logger = logger;
    }

    public async UnaryResult<CreateLobbyResponse> CreateLobbyAsync(CreateLobbyRequest request)
    {
        var userId = Context.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return new CreateLobbyResponse
            {
                Success = false,
                ErrorMessage = "User not authenticated",
            };
        }

        if (string.IsNullOrWhiteSpace(request.LobbyName) || request.LobbyName.Length > 50 ||
            string.IsNullOrWhiteSpace(request.GameMode) || request.GameMode.Length > 30 ||
            string.IsNullOrWhiteSpace(request.PlayerName) || request.PlayerName.Length > 50 ||
            request.MaxPlayers < 2 || request.MaxPlayers > 16)
        {
            return new CreateLobbyResponse
            {
                Success = false,
                ErrorMessage = "Invalid lobby parameters",
            };
        }

        try
        {
            var lobbyId = await _lobbyDataService.CreateAsync(
                userId, request.PlayerName, request.LobbyName, request.GameMode, request.MaxPlayers, request.IsPublic);

            _logger.LogInformation(
                "Lobby {LobbyId} created by {UserId} (mode: {GameMode})",
                lobbyId, userId, request.GameMode);

            return new CreateLobbyResponse
            {
                Success = true,
                LobbyId = lobbyId,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create lobby for user {UserId}", userId);
            return new CreateLobbyResponse
            {
                Success = false,
                ErrorMessage = "Failed to create lobby",
            };
        }
    }

    public async UnaryResult<LobbyInfo> JoinLobbyAsync(string lobbyId, string playerName)
    {
        var userId = Context.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.Unauthenticated, "User not authenticated");
        }

        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyId.Length > 64 ||
            string.IsNullOrWhiteSpace(playerName) || playerName.Length > 50)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.InvalidArgument, "Invalid parameters");
        }

        var added = await _lobbyDataService.AddPlayerAsync(lobbyId, userId, playerName);
        if (!added)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.FailedPrecondition, "Cannot join lobby");
        }

        var lobby = await _lobbyDataService.GetLobbyAsync(lobbyId);
        return lobby!;
    }

    public async UnaryResult<bool> LeaveLobbyAsync(string lobbyId)
    {
        var userId = Context.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.Unauthenticated, "User not authenticated");
        }

        return await _lobbyDataService.RemovePlayerAsync(lobbyId, userId);
    }

    public async UnaryResult<LobbyInfo[]> SearchLobbiesAsync(string gameMode, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(gameMode) || gameMode.Length > 30)
        {
            return Array.Empty<LobbyInfo>();
        }

        if (maxResults <= 0 || maxResults > 50)
        {
            maxResults = 10;
        }

        return await _lobbyDataService.SearchPublicAsync(gameMode, maxResults);
    }

    public async UnaryResult<LobbyInfo> GetLobbyInfoAsync(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyId.Length > 64)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.InvalidArgument, "Invalid lobby ID");
        }

        var lobby = await _lobbyDataService.GetLobbyAsync(lobbyId);
        if (lobby == null)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.NotFound, "Lobby not found");
        }

        return lobby;
    }

    public async UnaryResult<LobbyPlayerInfo[]> GetLobbyPlayersAsync(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyId.Length > 64)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.InvalidArgument, "Invalid lobby ID");
        }

        return await _lobbyDataService.GetPlayersAsync(lobbyId);
    }
}
