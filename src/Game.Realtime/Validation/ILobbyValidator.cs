using Game.Library.Shared.Dto;

namespace Game.Realtime.Validation;

public interface ILobbyValidator
{
    void ValidateLobbyId(string lobbyId);

    void ValidatePlayerName(string playerName);

    void ValidateCreateLobbyRequest(CreateLobbyRequest request);

    void ValidateGameMode(string gameMode);

    void ValidateLobbyMessage(string message);
}
