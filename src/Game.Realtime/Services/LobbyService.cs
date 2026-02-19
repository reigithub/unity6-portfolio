using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Services;
using Game.Realtime.Extensions;
using Game.Realtime.Validation;
using MagicOnion;
using MagicOnion.Server;

namespace Game.Realtime.Services;

/// <summary>
/// ロビー Unary RPC サービス実装
/// </summary>
public class LobbyService : ServiceBase<ILobbyService>, ILobbyService
{
    private readonly ILobbyDataService _lobbyDataService;
    private readonly ILobbyValidator _lobbyValidator;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(
        ILobbyDataService lobbyDataService,
        ILobbyValidator lobbyValidator,
        ILogger<LobbyService> logger)
    {
        _lobbyDataService = lobbyDataService;
        _lobbyValidator = lobbyValidator;
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

        _lobbyValidator.ValidateCreateLobbyRequest(request);

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

        _lobbyValidator.ValidateLobbyId(lobbyId);
        _lobbyValidator.ValidatePlayerName(playerName);

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
        _lobbyValidator.ValidateGameMode(gameMode);

        if (maxResults <= 0 || maxResults > 50)
        {
            maxResults = 10;
        }

        return await _lobbyDataService.SearchPublicAsync(gameMode, maxResults);
    }

    public async UnaryResult<LobbyInfo> GetLobbyInfoAsync(string lobbyId)
    {
        _lobbyValidator.ValidateLobbyId(lobbyId);

        var lobby = await _lobbyDataService.GetLobbyAsync(lobbyId);
        if (lobby == null)
        {
            throw new ReturnStatusException(Grpc.Core.StatusCode.NotFound, "Lobby not found");
        }

        return lobby;
    }

    public async UnaryResult<LobbyPlayerInfo[]> GetLobbyPlayersAsync(string lobbyId)
    {
        _lobbyValidator.ValidateLobbyId(lobbyId);

        return await _lobbyDataService.GetPlayersAsync(lobbyId);
    }
}
