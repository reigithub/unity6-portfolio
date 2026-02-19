using System.Collections.Concurrent;
using System.Security.Claims;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Enums;
using Game.Server.Services.Chat;
using Game.Server.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Game.Server.Hubs;

/// <summary>
/// チャット SignalR Hub 実装
/// 1接続で複数ルーム（Group）に同時参加可能
/// </summary>
[Authorize]
public class ChatHub : Hub<IChatHubClient>
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> ConnectionRooms = new();

    private readonly ILogger<ChatHub> _logger;
    private readonly IChatMessageService _chatMessageService;
    private readonly IChatRoomDataService _roomDataService;
    private readonly ChatPermissionValidator _validator;
    private readonly IChatInputValidator _chatInputValidator;

    public ChatHub(
        ILogger<ChatHub> logger,
        IChatMessageService chatMessageService,
        IChatRoomDataService roomDataService,
        ChatPermissionValidator validator,
        IChatInputValidator chatInputValidator)
    {
        _logger = logger;
        _chatMessageService = chatMessageService;
        _roomDataService = roomDataService;
        _validator = validator;
        _chatInputValidator = chatInputValidator;
    }

    private string GetUserId()
    {
        return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    }

    public async Task JoinAsync(string roomId, string playerName)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new HubException("User not authenticated");

        _chatInputValidator.ValidateRoomId(roomId);
        _chatInputValidator.ValidatePlayerName(playerName);

        await _validator.ValidateRoomExistsAsync(roomId);

        if (!await _validator.HasDefaultPermissionAsync(roomId, ChatRoomPermissions.Join))
        {
            throw new HubException("This room does not allow self-join");
        }

        var defaultPermissions = await _roomDataService.GetDefaultPermissionsAsync(roomId);
        var added = await _roomDataService.AddMemberAsync(roomId, userId, playerName, defaultPermissions);
        if (!added)
        {
            throw new HubException("Cannot join chat room (room full or does not exist)");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var rooms = ConnectionRooms.GetOrAdd(Context.ConnectionId, _ => new HashSet<string>());
        lock (rooms)
        {
            rooms.Add(roomId);
        }

        _logger.LogInformation(
            "Player {PlayerName} ({UserId}) joined chat room {RoomId}",
            playerName, userId, roomId);

        await Clients.Group(roomId).OnPlayerJoined(roomId, userId, playerName);
    }

    public async Task LeaveAsync(string roomId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new HubException("User not authenticated");

        _chatInputValidator.ValidateRoomId(roomId);

        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Leave);

        var member = await GetMemberPlayerNameAsync(roomId, userId);
        await _roomDataService.RemoveMemberAsync(roomId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        if (ConnectionRooms.TryGetValue(Context.ConnectionId, out var rooms))
        {
            lock (rooms)
            {
                rooms.Remove(roomId);
            }
        }

        _logger.LogInformation(
            "Player ({UserId}) left chat room {RoomId}",
            userId, roomId);

        await Clients.Group(roomId).OnPlayerLeft(roomId, userId, member);
    }

    public async Task SendMessageAsync(string roomId, string content)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new HubException("User not authenticated");

        _chatInputValidator.ValidateRoomId(roomId);
        _chatInputValidator.ValidateMessageContent(content);

        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.SendMessage);

        var playerName = await GetMemberPlayerNameAsync(roomId, userId);

        var message = new ChatMessage
        {
            UserId = userId,
            PlayerName = playerName,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        await _chatMessageService.SaveMessageAsync(roomId, message);

        _logger.LogDebug(
            "Player ({UserId}) sent chat message in room {RoomId}",
            userId, roomId);

        await Clients.Group(roomId).OnMessageReceived(roomId, message);
    }

    public async Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count)
    {
        _chatInputValidator.ValidateRoomId(roomId);
        _chatInputValidator.ValidateMessageCount(count);

        return await _chatMessageService.GetRecentMessagesAsync(roomId, count);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        if (ConnectionRooms.TryRemove(Context.ConnectionId, out var rooms))
        {
            string[] roomsCopy;
            lock (rooms)
            {
                roomsCopy = rooms.ToArray();
            }

            foreach (var roomId in roomsCopy)
            {
                var playerName = await GetMemberPlayerNameAsync(roomId, userId);
                await _roomDataService.RemoveMemberAsync(roomId, userId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
                await Clients.Group(roomId).OnPlayerLeft(roomId, userId, playerName);
            }
        }

        _logger.LogInformation(
            "Player ({UserId}) disconnected from chat hub",
            userId);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<string> GetMemberPlayerNameAsync(string roomId, string userId)
    {
        var members = await _roomDataService.GetMembersAsync(roomId);
        var member = members.FirstOrDefault(m => m.UserId == userId);
        return member?.PlayerName ?? "";
    }
}
