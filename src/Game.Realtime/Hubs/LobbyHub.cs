using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Grpc.Core;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Http;

namespace Game.Realtime.Hubs;

/// <summary>
/// ロビーHub サーバー実装
/// ロビー参加/退出は Unary ILobbyService 経由。Hub はリアルタイムイベント（チャット、レディ、ゲーム開始）専用。
/// </summary>
public class LobbyHub : StreamingHubBase<ILobbyHub, ILobbyHubReceiver>, ILobbyHub
{
    private readonly ILogger<LobbyHub> _logger;
    private readonly ILobbyDataService _lobbyDataService;

    private IGroup<ILobbyHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _lobbyId = string.Empty;

    public LobbyHub(ILogger<LobbyHub> logger, ILobbyDataService lobbyDataService)
    {
        _logger = logger;
        _lobbyDataService = lobbyDataService;
    }

    public async ValueTask ConnectAsync(string lobbyId, string playerName)
    {
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

        await _lobbyDataService.SetReadyAsync(_lobbyId, _userId, isReady);

        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerReadyChanged(_userId, isReady);
        }

        _logger.LogDebug(
            "Player {UserId} set ready={IsReady} in lobby {LobbyId}",
            _userId, isReady, _lobbyId);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerLeft(_userId, _playerName);
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
