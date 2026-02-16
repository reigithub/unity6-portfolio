using Game.Library.Shared.Realtime.Dto;
using Game.Library.Shared.Realtime.Services;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Http;

namespace Game.Realtime.Services;

/// <summary>
/// チャット Unary RPC サービス実装
/// ChatPermissionValidator 経由で権限チェックを行う
/// </summary>
public class ChatService : ServiceBase<IChatService>, IChatService
{
    private readonly IChatRoomDataService _roomDataService;
    private readonly IChatMessageService _chatMessageService;
    private readonly ChatPermissionValidator _validator;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IChatRoomDataService roomDataService,
        IChatMessageService chatMessageService,
        ChatPermissionValidator validator,
        ILogger<ChatService> logger)
    {
        _roomDataService = roomDataService;
        _chatMessageService = chatMessageService;
        _validator = validator;
        _logger = logger;
    }

    private string GetUserId()
    {
        return Context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value ?? "";
    }

    public async UnaryResult<CreateChatRoomResponse> CreateRoomAsync(CreateChatRoomRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return new CreateChatRoomResponse
            {
                Success = false,
                ErrorMessage = "User not authenticated",
            };
        }

        try
        {
            var roomId = await _roomDataService.CreateAsync(
                request.RoomName, request.RoomType, request.MaxMembers, request.DefaultPermissions);

            // 作成者を CreatorPermissions でメンバー追加
            await _roomDataService.AddMemberAsync(roomId, userId, request.RoomName, request.CreatorPermissions);

            _logger.LogInformation(
                "Chat room {RoomId} created by {UserId} (type: {RoomType})",
                roomId, userId, request.RoomType);

            return new CreateChatRoomResponse
            {
                Success = true,
                RoomId = roomId,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat room for user {UserId}", userId);
            return new CreateChatRoomResponse
            {
                Success = false,
                ErrorMessage = "Failed to create chat room",
            };
        }
    }

    public async UnaryResult<bool> DeleteRoomAsync(string roomId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new ReturnStatusException(StatusCode.Unauthenticated, "User not authenticated");

        await _validator.ValidateRoomExistsAsync(roomId);
        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Delete);

        await _roomDataService.DeleteAsync(roomId);
        await _chatMessageService.DeleteRoomAsync(roomId);

        _logger.LogInformation("Chat room {RoomId} deleted by {UserId}", roomId, userId);
        return true;
    }

    public async UnaryResult<bool> InviteMemberAsync(string roomId, string targetUserId, string playerName)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new ReturnStatusException(StatusCode.Unauthenticated, "User not authenticated");

        await _validator.ValidateRoomExistsAsync(roomId);
        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Invite);

        // 招待されたメンバーにはデフォルト権限を付与
        var defaultPermissions = await _roomDataService.GetDefaultPermissionsAsync(roomId);
        var added = await _roomDataService.AddMemberAsync(roomId, targetUserId, playerName, defaultPermissions);

        if (added)
        {
            _logger.LogInformation(
                "User {TargetUserId} invited to chat room {RoomId} by {UserId}",
                targetUserId, roomId, userId);
        }

        return added;
    }

    public async UnaryResult<bool> KickMemberAsync(string roomId, string targetUserId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new ReturnStatusException(StatusCode.Unauthenticated, "User not authenticated");

        await _validator.ValidateRoomExistsAsync(roomId);
        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Kick);

        var removed = await _roomDataService.RemoveMemberAsync(roomId, targetUserId);

        if (removed)
        {
            _logger.LogInformation(
                "User {TargetUserId} kicked from chat room {RoomId} by {UserId}",
                targetUserId, roomId, userId);
        }

        return removed;
    }

    public async UnaryResult<bool> SetMemberPermissionsAsync(string roomId, string targetUserId, int permissions)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new ReturnStatusException(StatusCode.Unauthenticated, "User not authenticated");

        await _validator.ValidateRoomExistsAsync(roomId);
        await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.ManageMember);

        var updated = await _roomDataService.SetMemberPermissionsAsync(roomId, targetUserId, permissions);

        if (updated)
        {
            _logger.LogInformation(
                "Permissions for {TargetUserId} in room {RoomId} updated by {UserId}",
                targetUserId, roomId, userId);
        }

        return updated;
    }

    public async UnaryResult<ChatRoomInfo> GetRoomInfoAsync(string roomId)
    {
        var room = await _roomDataService.GetRoomAsync(roomId);
        if (room == null)
            throw new ReturnStatusException(StatusCode.NotFound, "Chat room not found");

        return room;
    }

    public async UnaryResult<ChatRoomMemberInfo[]> GetRoomMembersAsync(string roomId)
    {
        await _validator.ValidateRoomExistsAsync(roomId);
        return await _roomDataService.GetMembersAsync(roomId);
    }
}
