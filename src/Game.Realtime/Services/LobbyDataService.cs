using System.Text.Json;
using Game.Library.Shared.Realtime.Dto;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのロビーデータ管理サービス
/// </summary>
public class LobbyDataService : ILobbyDataService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<LobbyDataService> _logger;

    public LobbyDataService(IConnectionMultiplexer redis, ILogger<LobbyDataService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> CreateAsync(
        string hostUserId, string playerName, string lobbyName, string gameMode, int maxPlayers, bool isPublic)
    {
        var db = _redis.GetDatabase();
        var lobbyId = Guid.NewGuid().ToString("N");

        var lobbyKey = $"lobby:{lobbyId}";
        var entries = new HashEntry[]
        {
            new("name", lobbyName),
            new("hostUserId", hostUserId),
            new("gameMode", gameMode),
            new("maxPlayers", maxPlayers),
            new("isPublic", isPublic ? "1" : "0"),
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };
        await db.HashSetAsync(lobbyKey, entries);

        // ホストをプレイヤーとして追加
        var playerData = JsonSerializer.Serialize(new { playerName, isReady = false, joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        await db.HashSetAsync($"lobby:{lobbyId}:players", hostUserId, playerData);

        // プレイヤーの現在ロビーを記録
        await db.StringSetAsync($"lobby:player:{hostUserId}", lobbyId);

        // 公開ロビー一覧に追加
        if (isPublic)
        {
            await db.SetAddAsync($"lobby:public:{gameMode}", lobbyId);
        }

        _logger.LogInformation(
            "Lobby {LobbyId} created by {HostUserId} (mode: {GameMode})",
            lobbyId, hostUserId, gameMode);

        return lobbyId;
    }

    public async Task<bool> AddPlayerAsync(string lobbyId, string userId, string playerName)
    {
        var db = _redis.GetDatabase();

        // ロビー存在チェック
        var exists = await db.KeyExistsAsync($"lobby:{lobbyId}");
        if (!exists) return false;

        // 最大人数チェック
        var maxPlayers = (int)await db.HashGetAsync($"lobby:{lobbyId}", "maxPlayers");
        var currentCount = await db.HashLengthAsync($"lobby:{lobbyId}:players");
        if (currentCount >= maxPlayers) return false;

        // 多重参加防止
        var currentLobby = await db.StringGetAsync($"lobby:player:{userId}");
        if (currentLobby.HasValue) return false;

        var playerData = JsonSerializer.Serialize(new { playerName, isReady = false, joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        await db.HashSetAsync($"lobby:{lobbyId}:players", userId, playerData);
        await db.StringSetAsync($"lobby:player:{userId}", lobbyId);

        _logger.LogDebug("Player {UserId} added to lobby {LobbyId}", userId, lobbyId);
        return true;
    }

    public async Task<bool> RemovePlayerAsync(string lobbyId, string userId)
    {
        var db = _redis.GetDatabase();

        var removed = await db.HashDeleteAsync($"lobby:{lobbyId}:players", userId);
        if (!removed) return false;

        await db.KeyDeleteAsync($"lobby:player:{userId}");

        // プレイヤーがいなくなったらロビーを削除
        var remainingPlayers = await db.HashLengthAsync($"lobby:{lobbyId}:players");
        if (remainingPlayers == 0)
        {
            await DeleteAsync(lobbyId);
        }

        _logger.LogDebug("Player {UserId} removed from lobby {LobbyId}", userId, lobbyId);
        return true;
    }

    public async Task<LobbyInfo?> GetLobbyAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"lobby:{lobbyId}");
        if (hash.Length == 0) return null;

        var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
        var playerCount = await db.HashLengthAsync($"lobby:{lobbyId}:players");

        return new LobbyInfo
        {
            LobbyId = lobbyId,
            LobbyName = dict.GetValueOrDefault("name", ""),
            HostUserId = dict.GetValueOrDefault("hostUserId", ""),
            GameMode = dict.GetValueOrDefault("gameMode", ""),
            CurrentPlayers = (int)playerCount,
            MaxPlayers = int.TryParse(dict.GetValueOrDefault("maxPlayers", "4"), out var mp) ? mp : 4,
            IsPublic = dict.GetValueOrDefault("isPublic", "0") == "1",
        };
    }

    public async Task<LobbyPlayerInfo[]> GetPlayersAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"lobby:{lobbyId}:players");
        var hostUserId = (string?)await db.HashGetAsync($"lobby:{lobbyId}", "hostUserId") ?? "";

        var players = new LobbyPlayerInfo[hash.Length];
        for (var i = 0; i < hash.Length; i++)
        {
            var userId = hash[i].Name.ToString();
            var data = JsonSerializer.Deserialize<PlayerData>(hash[i].Value.ToString());
            players[i] = new LobbyPlayerInfo
            {
                UserId = userId,
                PlayerName = data?.playerName ?? "",
                IsReady = data?.isReady ?? false,
                IsHost = userId == hostUserId,
            };
        }

        return players;
    }

    public async Task<LobbyInfo[]> SearchPublicAsync(string gameMode, int maxResults)
    {
        var db = _redis.GetDatabase();
        var lobbyIds = await db.SetMembersAsync($"lobby:public:{gameMode}");

        var results = new List<LobbyInfo>();
        foreach (var id in lobbyIds)
        {
            if (results.Count >= maxResults) break;

            var lobby = await GetLobbyAsync(id.ToString());
            if (lobby != null && lobby.CurrentPlayers < lobby.MaxPlayers)
            {
                results.Add(lobby);
            }
        }

        return results.ToArray();
    }

    public async Task<bool> SetReadyAsync(string lobbyId, string userId, bool isReady)
    {
        var db = _redis.GetDatabase();
        var raw = await db.HashGetAsync($"lobby:{lobbyId}:players", userId);
        if (!raw.HasValue) return false;

        var data = JsonSerializer.Deserialize<PlayerData>(raw.ToString());
        if (data == null) return false;

        data.isReady = isReady;
        await db.HashSetAsync($"lobby:{lobbyId}:players", userId, JsonSerializer.Serialize(data));
        return true;
    }

    public async Task<bool> AreAllReadyAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"lobby:{lobbyId}:players");

        foreach (var entry in hash)
        {
            var data = JsonSerializer.Deserialize<PlayerData>(entry.Value.ToString());
            if (data is not { isReady: true }) return false;
        }

        return hash.Length > 0;
    }

    public async Task DeleteAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();

        // ゲームモード取得（公開ロビー一覧から削除するため）
        var gameMode = (string?)await db.HashGetAsync($"lobby:{lobbyId}", "gameMode");

        // プレイヤーの参加記録を削除
        var playerEntries = await db.HashGetAllAsync($"lobby:{lobbyId}:players");
        foreach (var entry in playerEntries)
        {
            await db.KeyDeleteAsync($"lobby:player:{entry.Name}");
        }

        // ロビーデータ削除
        await db.KeyDeleteAsync($"lobby:{lobbyId}");
        await db.KeyDeleteAsync($"lobby:{lobbyId}:players");

        // 公開ロビー一覧から削除
        if (!string.IsNullOrEmpty(gameMode))
        {
            await db.SetRemoveAsync($"lobby:public:{gameMode}", lobbyId);
        }

        _logger.LogInformation("Lobby {LobbyId} deleted", lobbyId);
    }

    public async Task<string?> GetPlayerLobbyAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var lobbyId = await db.StringGetAsync($"lobby:player:{userId}");
        return lobbyId.HasValue ? lobbyId.ToString() : null;
    }

    // JSON デシリアライズ用の内部クラス
    private class PlayerData
    {
        public string playerName { get; set; } = "";
        public bool isReady { get; set; }
        public long joinedAt { get; set; }
    }
}
