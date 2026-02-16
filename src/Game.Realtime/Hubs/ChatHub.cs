using Game.Library.Shared.Realtime.Hubs;
using Grpc.Core;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Hubs;

/// <summary>
/// チャットHub サーバー実装
/// </summary>
public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ILogger<ChatHub> _logger;

    private IGroup<IChatHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _roomId = string.Empty;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public async ValueTask JoinAsync(string roomId, string playerName)
    {
        _userId = Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value
            ?? ConnectionId.ToString();
        _playerName = playerName;
        _roomId = roomId;

        _currentGroup = await Group.AddAsync($"chat:{roomId}");

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) joined chat room {RoomId}",
            playerName,
            _userId,
            roomId);

        _currentGroup.All.OnPlayerJoined(_userId, playerName);
    }

    public async ValueTask LeaveAsync()
    {
        if (_currentGroup != null)
        {
            _logger.LogInformation(
                "Player {PlayerName} ({UserId}) left chat room {RoomId}",
                _playerName,
                _userId,
                _roomId);

            _currentGroup.All.OnPlayerLeft(_userId, _playerName);
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }
    }

    public ValueTask SendMessageAsync(string content)
    {
        if (_currentGroup != null)
        {
            var message = new ChatMessage
            {
                UserId = _userId,
                PlayerName = _playerName,
                Content = content,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            _logger.LogDebug(
                "Player {PlayerName} sent chat message in room {RoomId}",
                _playerName,
                _roomId);

            _currentGroup.All.OnMessageReceived(message);
        }

        return default;
    }

    public ValueTask<ChatMessage[]> GetRecentMessagesAsync(int count)
    {
        // TODO: Valkey からメッセージ履歴を取得する実装
        return new ValueTask<ChatMessage[]>(Array.Empty<ChatMessage>());
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
            "Player {PlayerName} ({UserId}) disconnected from chat room {RoomId}",
            _playerName,
            _userId,
            _roomId);

        return default;
    }
}
