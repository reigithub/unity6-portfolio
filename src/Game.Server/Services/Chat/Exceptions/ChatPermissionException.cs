using Game.Server.Shared.Exceptions;

namespace Game.Server.Services.Chat.Exceptions;

/// <summary>
/// チャットルーム権限不足時にスローされる例外
/// </summary>
public class ChatPermissionException : ErrorException
{
    public ChatPermissionException(string message)
        : base("PERMISSION_DENIED", message, 403)
    {
    }
}
