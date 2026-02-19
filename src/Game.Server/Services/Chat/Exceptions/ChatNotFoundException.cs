using Game.Server.Shared.Exceptions;

namespace Game.Server.Services.Chat.Exceptions;

/// <summary>
/// チャットルーム/メンバーが見つからない時にスローされる例外
/// </summary>
public class ChatNotFoundException : ErrorException
{
    public ChatNotFoundException(string message)
        : base("NOT_FOUND", message, 404)
    {
    }
}
