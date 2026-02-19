using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Grpc.Core;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Game.Realtime.Hubs;

/// <summary>
/// ロビーHub サーバー実装
/// ロビー参加/退出は Unary ILobbyService 経由。Hub はリアルタイムイベント（チャット、レディ、ゲーム開始）専用。
/// </summary>
public class LobbyHub : StreamingHubBase<ILobbyHub, ILobbyHubReceiver>, ILobbyHub
{
    private readonly ILogger<LobbyHub> _logger;
    private readonly ILobbyDataService _lobbyDataService;
    private readonly IMatchSessionTokenService _tokenService;
    private readonly GameServerConfiguration _gameServerConfig;

    private IGroup<ILobbyHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _lobbyId = string.Empty;

    public LobbyHub(
        ILogger<LobbyHub> logger,
        ILobbyDataService lobbyDataService,
        IMatchSessionTokenService tokenService,
        IOptions<GameServerConfiguration> gameServerConfig)
    {
        _logger = logger;
        _lobbyDataService = lobbyDataService;
        _tokenService = tokenService;
        _gameServerConfig = gameServerConfig.Value;
    }

    public async ValueTask ConnectAsync(string lobbyId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyId.Length > 64 ||
            string.IsNullOrWhiteSpace(playerName) || playerName.Length > 50)
        {
            return;
        }

        _userId = Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value
            ?? ConnectionId.ToString();
        _playerName = playerName;
        _lobbyId = lobbyId;

        _currentGroup = await Group.AddAsync(lobbyId);

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) connected to lobby {LobbyId}",
            playerName, _userId, lobbyId);

        _currentGroup.All.OnPlayerJoined(_userId, playerName);
    }

    public async ValueTask LeaveAsync()
    {
        if (_currentGroup != null)
        {
            _logger.LogInformation(
                "Player {PlayerName} ({UserId}) left lobby {LobbyId}",
                _playerName, _userId, _lobbyId);

            _currentGroup.All.OnPlayerLeft(_userId, _playerName);

            // ホスト退出時はロビーを閉じる
            var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
            if (lobby != null && lobby.HostUserId == _userId)
            {
                _currentGroup.All.OnLobbyClosed("Host left");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;

            if (!string.IsNullOrEmpty(_lobbyId))
            {
                await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
            }
        }
    }

    public ValueTask SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200)
        {
            return default;
        }

        if (_currentGroup != null)
        {
            _logger.LogDebug("Player {PlayerName} sent message in lobby {LobbyId}", _playerName, _lobbyId);
            _currentGroup.All.OnMessageReceived(_userId, _playerName, message);
        }

        return default;
    }

    public async ValueTask SetReadyAsync(bool isReady)
    {
        if (string.IsNullOrEmpty(_lobbyId)) return;

        var (success, allReady) = await _lobbyDataService.SetReadyAndCheckAllAsync(_lobbyId, _userId, isReady);
        if (!success) return;

        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerReadyChanged(_userId, isReady);
        }

        // 全員 Ready チェック → ゲーム開始（SetReady と AllReady はアトミック）
        if (isReady && allReady && _currentGroup != null)
        {
            await StartGameAsync();
        }

        _logger.LogDebug(
            "Player {UserId} set ready={IsReady} in lobby {LobbyId}",
            _userId, isReady, _lobbyId);
    }

    private async ValueTask StartGameAsync()
    {
        var matchId = Guid.NewGuid().ToString("N");
        var players = await _lobbyDataService.GetPlayersAsync(_lobbyId);

        foreach (var player in players)
        {
            await _tokenService.IssueTokenAsync(player.UserId, matchId);
        }

        _currentGroup!.All.OnGameStarting(matchId, _gameServerConfig.ServerAddress, _gameServerConfig.ServerPort);

        _logger.LogInformation(
            "Game starting from lobby {LobbyId}: match {MatchId} with {PlayerCount} players",
            _lobbyId, matchId, players.Length);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerLeft(_userId, _playerName);

            // ホスト退出時はロビーを閉じる
            var lobby = await _lobbyDataService.GetLobbyAsync(_lobbyId);
            if (lobby != null && lobby.HostUserId == _userId)
            {
                _currentGroup.All.OnLobbyClosed("Host disconnected");
            }

            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        if (!string.IsNullOrEmpty(_lobbyId))
        {
            await _lobbyDataService.RemovePlayerAsync(_lobbyId, _userId);
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) disconnected from lobby {LobbyId}",
            _playerName, _userId, _lobbyId);
    }
}
