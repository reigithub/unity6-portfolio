using Game.Server.Shared.Exceptions;

namespace Game.Server.Validation;

public class ChatInputValidator : IChatInputValidator
{
    public void ValidateRoomId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > 64)
        {
            throw new ErrorException(
                "INVALID_INPUT",
                "Room ID is required and must not exceed 64 characters.");
        }
    }

    public void ValidatePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 50)
        {
            throw new ErrorException(
                "INVALID_INPUT",
                "Player name is required and must not exceed 50 characters.");
        }
    }

    public void ValidateMessageContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
        {
            throw new ErrorException(
                "INVALID_INPUT",
                "Message content is required and must not exceed 500 characters.");
        }
    }

    public void ValidateMessageCount(int count)
    {
        if (count <= 0 || count > 100)
        {
            throw new ErrorException(
                "INVALID_INPUT",
                "Message count must be between 1 and 100.");
        }
    }
}
