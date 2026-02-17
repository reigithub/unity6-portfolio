using System;
using System.Collections.Generic;

namespace Game.Shared.Dto.Chat
{
    // ============================================================
    // Request DTOs
    // ============================================================

    [Serializable]
    public class CreateChatRoomRestRequest
    {
        public string roomName;
        public string roomType;
        public int maxMembers;
        public int defaultPermissions;
        public int creatorPermissions;
    }

    [Serializable]
    public class InviteMemberRequest
    {
        public string targetUserId;
        public string playerName;
    }

    [Serializable]
    public class SetPermissionsRequest
    {
        public int permissions;
    }

    // ============================================================
    // Response DTOs
    // ============================================================

    [Serializable]
    public class CreateChatRoomRestResponse
    {
        public bool success;
        public string roomId;
        public string errorMessage;
    }

    [Serializable]
    public class ChatRoomInfoResponse
    {
        public string roomId;
        public string roomName;
        public string roomType;
        public int currentMembers;
        public int maxMembers;
        public long createdAt;
        public int defaultPermissions;
    }

    [Serializable]
    public class ChatRoomMemberInfoResponse
    {
        public string userId;
        public string playerName;
        public long joinedAt;
        public int permissions;
    }

    [Serializable]
    public class ChatOperationResponse
    {
        public bool success;
    }

    [Serializable]
    public class ChatRoomMembersResponse
    {
        public List<ChatRoomMemberInfoResponse> members;
    }
}
