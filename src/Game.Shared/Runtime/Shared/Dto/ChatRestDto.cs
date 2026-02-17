using System;
using System.ComponentModel.DataAnnotations;
using Game.Library.Shared.Chat.Dto;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class CreateChatRoomRequest
    {
        [Required]
        public string RoomName { get; set; } = string.Empty;

        [Required]
        public string RoomType { get; set; } = string.Empty;

        public int MaxMembers { get; set; }

        public int DefaultPermissions { get; set; }

        public int CreatorPermissions { get; set; }
    }

    [MessagePackObject(true)]
    public class InviteMemberRequest
    {
        [Required]
        public string TargetUserId { get; set; } = string.Empty;

        [Required]
        public string PlayerName { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class SetPermissionsRequest
    {
        public int Permissions { get; set; }
    }

    [MessagePackObject(true)]
    public class CreateChatRoomResponse
    {
        public bool Success { get; set; }

        public string RoomId { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class ChatRoomMembersResponse
    {
        public ChatRoomMemberInfo[] Members { get; set; } = Array.Empty<ChatRoomMemberInfo>();
    }
}
