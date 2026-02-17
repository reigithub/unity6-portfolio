using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests.Chat;

public class CreateChatRoomRestRequest
{
    [Required]
    public string RoomName { get; set; } = string.Empty;

    [Required]
    public string RoomType { get; set; } = string.Empty;

    public int MaxMembers { get; set; }

    public int DefaultPermissions { get; set; }

    public int CreatorPermissions { get; set; }
}

public class InviteMemberRequest
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;

    [Required]
    public string PlayerName { get; set; } = string.Empty;
}

public class SetPermissionsRequest
{
    public int Permissions { get; set; }
}
