namespace Game.Server.Services.Chat.Exceptions;

/// <summary>
/// チャットルーム/メンバーが見つからない時にスローされる例外
/// </summary>
public class ChatNotFoundException : Exception
{
    public ChatNotFoundException(string message)
        : base(message)
    {
    }
}
