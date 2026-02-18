using System;
using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class CreateChatRoomRequest
    {
        [Key(0)]
        [Required]
        public string RoomName { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        public string RoomType { get; set; } = string.Empty;

        [Key(2)]
        public int MaxMembers { get; set; }

        [Key(3)]
        public int DefaultPermissions { get; set; }

        [Key(4)]
        public int CreatorPermissions { get; set; }
    }

    [MessagePackObject]
    public class InviteMemberRequest
    {
        [Key(0)]
        [Required]
        public string TargetUserId { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        public string PlayerName { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class SetPermissionsRequest
    {
        [Key(0)]
        public int Permissions { get; set; }
    }

    [MessagePackObject]
    public class CreateChatRoomResponse
    {
        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public string RoomId { get; set; } = string.Empty;

        [Key(2)]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class ChatRoomMembersResponse
    {
        [Key(0)]
        public ChatRoomMemberInfo[] Members { get; set; } = Array.Empty<ChatRoomMemberInfo>();
    }
}
