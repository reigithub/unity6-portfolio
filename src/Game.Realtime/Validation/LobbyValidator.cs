using Game.Library.Shared.Dto;
using Game.Server.Shared.Exceptions;

namespace Game.Realtime.Validation;

public class LobbyValidator : ILobbyValidator
{
    public void ValidateLobbyId(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || lobbyId.Length > 64)
        {
            throw new ErrorException(
                "INVALID_LOBBY_ID",
                "Lobby ID is required and must not exceed 64 characters.");
        }
    }

    public void ValidatePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 50)
        {
            throw new ErrorException(
                "INVALID_PLAYER_NAME",
                "Player name is required and must not exceed 50 characters.");
        }
    }

    public void ValidateCreateLobbyRequest(CreateLobbyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LobbyName) || request.LobbyName.Length > 50)
        {
            throw new ErrorException(
                "INVALID_LOBBY_REQUEST",
                "Lobby name is required and must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.GameMode) || request.GameMode.Length > 30)
        {
            throw new ErrorException(
                "INVALID_LOBBY_REQUEST",
                "Game mode is required and must not exceed 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.PlayerName) || request.PlayerName.Length > 50)
        {
            throw new ErrorException(
                "INVALID_LOBBY_REQUEST",
                "Player name is required and must not exceed 50 characters.");
        }

        if (request.MaxPlayers < 2 || request.MaxPlayers > 16)
        {
            throw new ErrorException(
                "INVALID_LOBBY_REQUEST",
                "Max players must be between 2 and 16.");
        }
    }

    public void ValidateGameMode(string gameMode)
    {
        if (string.IsNullOrWhiteSpace(gameMode) || gameMode.Length > 30)
        {
            throw new ErrorException(
                "INVALID_GAME_MODE",
                "Game mode is required and must not exceed 30 characters.");
        }
    }

    public void ValidateLobbyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200)
        {
            throw new ErrorException(
                "INVALID_MESSAGE",
                "Message is required and must not exceed 200 characters.");
        }
    }
}
