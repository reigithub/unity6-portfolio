namespace Game.Server.Services.Chat.Exceptions;

/// <summary>
/// チャットルーム権限不足時にスローされる例外
/// </summary>
public class ChatPermissionException : Exception
{
    public ChatPermissionException(string message)
        : base(message)
    {
    }
}
