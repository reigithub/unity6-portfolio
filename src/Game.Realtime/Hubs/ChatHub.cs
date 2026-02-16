using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.Realtime.Services;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;

namespace Game.Realtime.Hubs;

/// <summary>
/// チャットHub サーバー実装
/// メッセージ送信時に Valkey へ永続化し、履歴取得にも対応する
/// 権限チェックは ChatPermissionValidator を介して行う
/// </summary>
public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IChatMessageService _chatMessageService;
    private readonly IChatRoomDataService _roomDataService;
    private readonly ChatPermissionValidator _validator;

    private IGroup<IChatHubReceiver>? _currentGroup;
    private string _userId = string.Empty;
    private string _playerName = string.Empty;
    private string _roomId = string.Empty;

    public ChatHub(
        ILogger<ChatHub> logger,
        IChatMessageService chatMessageService,
        IChatRoomDataService roomDataService,
        ChatPermissionValidator validator)
    {
        _logger = logger;
        _chatMessageService = chatMessageService;
        _roomDataService = roomDataService;
        _validator = validator;
    }

    public async ValueTask JoinAsync(string roomId, string playerName)
    {
        _userId = Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value
            ?? ConnectionId.ToString();
        _playerName = playerName;
        _roomId = roomId;

        // ルーム存在確認
        await _validator.ValidateRoomExistsAsync(roomId);

        // デフォルト権限で Join が許可されているか確認
        if (!await _validator.HasDefaultPermissionAsync(roomId, ChatRoomPermissions.Join))
        {
            throw new ReturnStatusException(StatusCode.PermissionDenied,
                "This room does not allow self-join");
        }

        // デフォルト権限でメンバー追加
        var defaultPermissions = await _roomDataService.GetDefaultPermissionsAsync(roomId);
        var added = await _roomDataService.AddMemberAsync(roomId, _userId, playerName, defaultPermissions);
        if (!added)
        {
            throw new ReturnStatusException(StatusCode.FailedPrecondition,
                "Cannot join chat room (room full or does not exist)");
        }

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
            // Leave 権限チェック
            await _validator.ValidateAsync(_roomId, _userId, ChatRoomPermissions.Leave);

            await _roomDataService.RemoveMemberAsync(_roomId, _userId);

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
            // SendMessage 権限チェック
            await _validator.ValidateAsync(_roomId, _userId, ChatRoomPermissions.SendMessage);

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

    protected override async ValueTask OnDisconnected()
    {
        if (_currentGroup != null)
        {
            // 接続断は強制退出（権限チェックなし）
            await _roomDataService.RemoveMemberAsync(_roomId, _userId);

            _currentGroup.All.OnPlayerLeft(_userId, _playerName);
            await _currentGroup.RemoveAsync(Context);
            _currentGroup = null;
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) disconnected from chat room {RoomId}",
            _playerName,
            _userId,
            _roomId);
    }
}
