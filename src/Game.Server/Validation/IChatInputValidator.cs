namespace Game.Server.Validation;

public interface IChatInputValidator
{
    void ValidateRoomId(string roomId);

    void ValidatePlayerName(string playerName);

    void ValidateMessageContent(string content);

    void ValidateMessageCount(int count);
}
