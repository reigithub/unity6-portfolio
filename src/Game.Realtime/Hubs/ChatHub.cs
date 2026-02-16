using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Grpc.Core;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Hubs;

/// <summary>
/// チャットHub サーバー実装
/// メッセージ送信時に Valkey へ永続化し、履歴取得にも対応する
/// </summary>
public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IChatMessageService _chatMessageService;

    private IGroup<IChatHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _roomId = string.Empty;

    public ChatHub(ILogger<ChatHub> logger, IChatMessageService chatMessageService)
    {
        _logger = logger;
        _chatMessageService = chatMessageService;
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

    public async ValueTask SendMessageAsync(string content)
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

            // Valkey に永続化
            await _chatMessageService.SaveMessageAsync(_roomId, message);

            _logger.LogDebug(
                "Player {PlayerName} sent chat message in room {RoomId}",
                _playerName,
                _roomId);

            _currentGroup.All.OnMessageReceived(message);
        }
    }

    public async ValueTask<ChatMessage[]> GetRecentMessagesAsync(int count)
    {
        if (string.IsNullOrEmpty(_roomId))
        {
            return Array.Empty<ChatMessage>();
        }

        return await _chatMessageService.GetRecentMessagesAsync(_roomId, count);
    }

    public async ValueTask DeleteRoomMessagesAsync()
    {
        if (!string.IsNullOrEmpty(_roomId))
        {
            await _chatMessageService.DeleteRoomAsync(_roomId);
        }
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
