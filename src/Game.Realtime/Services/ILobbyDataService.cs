using Game.Library.Shared.Realtime.Dto;

namespace Game.Realtime.Services;

/// <summary>
/// ロビー Valkey データ管理サービスインターフェース
/// </summary>
public interface ILobbyDataService
{
    Task<string> CreateAsync(string hostUserId, string playerName, string lobbyName, string gameMode, int maxPlayers, bool isPublic);
    Task<bool> AddPlayerAsync(string lobbyId, string userId, string playerName);
    Task<bool> RemovePlayerAsync(string lobbyId, string userId);
    Task<LobbyInfo?> GetLobbyAsync(string lobbyId);
    Task<LobbyPlayerInfo[]> GetPlayersAsync(string lobbyId);
    Task<LobbyInfo[]> SearchPublicAsync(string gameMode, int maxResults);
    Task<bool> SetReadyAsync(string lobbyId, string userId, bool isReady);
    Task<bool> AreAllReadyAsync(string lobbyId);
    Task DeleteAsync(string lobbyId);
    Task<string?> GetPlayerLobbyAsync(string userId);
}
