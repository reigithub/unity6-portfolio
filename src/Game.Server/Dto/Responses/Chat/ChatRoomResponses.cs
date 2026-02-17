using Game.Library.Shared.Chat.Dto;

namespace Game.Server.Dto.Responses.Chat;

public class CreateChatRoomRestResponse
{
    public bool Success { get; set; }

    public string RoomId { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}

public class ChatRoomMembersResponse
{
    public ChatRoomMemberInfo[] Members { get; set; } = Array.Empty<ChatRoomMemberInfo>();
}
