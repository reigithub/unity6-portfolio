using Game.Library.Shared.Realtime.Hubs;
using Grpc.Core;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Hubs;

/// <summary>
/// ロビーHub サーバー実装
/// </summary>
public class LobbyHub : StreamingHubBase<ILobbyHub, ILobbyHubReceiver>, ILobbyHub
{
    private readonly ILogger<LobbyHub> _logger;

    private IGroup<ILobbyHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;

    public LobbyHub(ILogger<LobbyHub> logger)
    {
        _logger = logger;
    }

    public async ValueTask JoinAsync(string lobbyId, string playerName)
    {
        _userId = Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value
            ?? ConnectionId.ToString();
        _playerName = playerName;

        _currentGroup = await Group.AddAsync(lobbyId);

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) joined lobby {LobbyId}",
            playerName,
            _userId,
            lobbyId);

        _currentGroup.All.OnPlayerJoined(_userId, playerName);
    }

    public async ValueTask LeaveAsync()
    {
        if (_currentGroup != null)
        {
            _logger.LogInformation(
                "Player {PlayerName} ({UserId}) left lobby",
                _playerName,
                _userId);

            _currentGroup.All.OnPlayerLeft(_userId, _playerName);
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }
    }

    public ValueTask SendMessageAsync(string message)
    {
        if (_currentGroup != null)
        {
            _logger.LogDebug("Player {PlayerName} sent message in lobby", _playerName);
            _currentGroup.All.OnMessageReceived(_userId, _playerName, message);
        }

        return default;
    }

    public ValueTask<string[]> GetPlayersAsync()
    {
        // TODO: Valkey からプレイヤー一覧を取得する実装
        return new ValueTask<string[]>(Array.Empty<string>());
    }

    protected override ValueTask OnDisconnected()
    {
        if (_currentGroup != null)
        {
            _currentGroup.All.OnPlayerLeft(_userId, _playerName);
            _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) disconnected from lobby",
            _playerName,
            _userId);

        return default;
    }
}
