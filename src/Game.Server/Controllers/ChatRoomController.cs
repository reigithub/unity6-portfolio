using System.Security.Claims;
using Game.Library.Shared.Chat.Dto;
using Game.Server.Dto.Requests.Chat;
using Game.Server.Hubs;
using Game.Server.Services.Chat;
using Game.Server.Services.Chat.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/chat/rooms")]
[Authorize]
public class ChatRoomController : ControllerBase
{
    private readonly IChatRoomDataService _roomDataService;
    private readonly IChatMessageService _chatMessageService;
    private readonly ChatPermissionValidator _validator;
    private readonly IHubContext<ChatHub, IChatHubClient> _hubContext;
    private readonly ILogger<ChatRoomController> _logger;

    public ChatRoomController(
        IChatRoomDataService roomDataService,
        IChatMessageService chatMessageService,
        ChatPermissionValidator validator,
        IHubContext<ChatHub, IChatHubClient> hubContext,
        ILogger<ChatRoomController> logger)
    {
        _roomDataService = roomDataService;
        _chatMessageService = chatMessageService;
        _validator = validator;
        _hubContext = hubContext;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateChatRoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateChatRoomRestRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var roomId = await _roomDataService.CreateAsync(
                request.RoomName, request.RoomType, request.MaxMembers, request.DefaultPermissions);

            await _roomDataService.AddMemberAsync(roomId, userId, request.RoomName, request.CreatorPermissions);

            _logger.LogInformation(
                "Chat room {RoomId} created by {UserId} (type: {RoomType})",
                roomId, userId, request.RoomType);

            return Ok(new CreateChatRoomResponse
            {
                Success = true,
                RoomId = roomId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat room for user {UserId}", userId);
            return Ok(new CreateChatRoomResponse
            {
                Success = false,
                ErrorMessage = "Failed to create chat room",
            });
        }
    }

    [HttpDelete("{roomId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoom(string roomId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _validator.ValidateRoomExistsAsync(roomId);
            await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Delete);

            await _roomDataService.DeleteAsync(roomId);
            await _chatMessageService.DeleteRoomAsync(roomId);

            await _hubContext.Clients.Group(roomId).OnRoomDeleted(roomId, "Room deleted by owner");

            _logger.LogInformation("Chat room {RoomId} deleted by {UserId}", roomId, userId);
            return Ok(new { success = true });
        }
        catch (ChatNotFoundException)
        {
            return NotFound(new { message = "Chat room not found" });
        }
        catch (ChatPermissionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Missing permission: Delete" });
        }
    }

    [HttpGet("{roomId}")]
    [ProducesResponseType(typeof(ChatRoomInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomInfo(string roomId)
    {
        var room = await _roomDataService.GetRoomAsync(roomId);
        if (room == null)
            return NotFound(new { message = "Chat room not found" });

        return Ok(room);
    }

    [HttpGet("{roomId}/members")]
    [ProducesResponseType(typeof(ChatRoomMemberInfo[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(string roomId)
    {
        try
        {
            await _validator.ValidateRoomExistsAsync(roomId);
            var members = await _roomDataService.GetMembersAsync(roomId);
            return Ok(members);
        }
        catch (ChatNotFoundException)
        {
            return NotFound(new { message = "Chat room not found" });
        }
    }

    [HttpPost("{roomId}/invite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteMember(string roomId, [FromBody] InviteMemberRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _validator.ValidateRoomExistsAsync(roomId);
            await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Invite);

            var defaultPermissions = await _roomDataService.GetDefaultPermissionsAsync(roomId);
            var added = await _roomDataService.AddMemberAsync(
                roomId, request.TargetUserId, request.PlayerName, defaultPermissions);

            if (added)
            {
                _logger.LogInformation(
                    "User {TargetUserId} invited to chat room {RoomId} by {UserId}",
                    request.TargetUserId, roomId, userId);
            }

            return Ok(new { success = added });
        }
        catch (ChatNotFoundException)
        {
            return NotFound(new { message = "Chat room not found" });
        }
        catch (ChatPermissionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Missing permission: Invite" });
        }
    }

    [HttpPost("{roomId}/kick")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> KickMember(string roomId, [FromBody] InviteMemberRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _validator.ValidateRoomExistsAsync(roomId);
            await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.Kick);

            var removed = await _roomDataService.RemoveMemberAsync(roomId, request.TargetUserId);

            if (removed)
            {
                await _hubContext.Clients.Group(roomId)
                    .OnPlayerLeft(roomId, request.TargetUserId, request.PlayerName);

                _logger.LogInformation(
                    "User {TargetUserId} kicked from chat room {RoomId} by {UserId}",
                    request.TargetUserId, roomId, userId);
            }

            return Ok(new { success = removed });
        }
        catch (ChatNotFoundException)
        {
            return NotFound(new { message = "Chat room not found" });
        }
        catch (ChatPermissionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Missing permission: Kick" });
        }
    }

    [HttpPost("{roomId}/members/{targetUserId}/permissions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(
        string roomId,
        string targetUserId,
        [FromBody] SetPermissionsRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _validator.ValidateRoomExistsAsync(roomId);
            await _validator.ValidateAsync(roomId, userId, ChatRoomPermissions.ManageMember);

            var updated = await _roomDataService.SetMemberPermissionsAsync(
                roomId, targetUserId, request.Permissions);

            if (updated)
            {
                await _hubContext.Clients.Group(roomId)
                    .OnPermissionsChanged(roomId, request.Permissions);

                _logger.LogInformation(
                    "Permissions for {TargetUserId} in room {RoomId} updated by {UserId}",
                    targetUserId, roomId, userId);
            }

            return Ok(new { success = updated });
        }
        catch (ChatNotFoundException)
        {
            return NotFound(new { message = "Chat room not found" });
        }
        catch (ChatPermissionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Missing permission: ManageMember" });
        }
    }
}
