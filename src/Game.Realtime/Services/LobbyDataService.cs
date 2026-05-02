using Game.Library.Shared.Dto;
using Game.Server.Shared.Extensions;
using Game.Server.Shared.Valkey;
using Medallion.Threading;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのロビーデータ管理サービス
/// </summary>
public class LobbyDataService : ILobbyDataService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<LobbyDataService> _logger;

    public LobbyDataService(
        IConnectionMultiplexer redis,
        IDistributedLockProvider lockProvider,
        ILogger<LobbyDataService> logger)
    {
        _redis = redis;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    public async Task<string?> CreateAsync(
        string hostUserId, string playerName, string lobbyName, string gameMode, int maxPlayers, bool isPublic, int stageId = 1)
    {
        var db = _redis.GetDatabase();
        var lobbyId = Guid.NewGuid().ToString("N");

        // ホストが既にロビーに参加中かチェック（原子的）
        var set = await db.StringSetAsync($"lobby:player:{hostUserId}", lobbyId, when: When.NotExists);
        if (!set)
        {
            _logger.LogWarning("Player {HostUserId} is already in a lobby, cannot create new one", hostUserId);
            return null;
        }

        var lobbyKey = $"lobby:{lobbyId}";
        var entries = new HashEntry[]
        {
            new("name", lobbyName),
            new("hostUserId", hostUserId),
            new("gameMode", gameMode),
            new("maxPlayers", maxPlayers),
            new("isPublic", isPublic ? "1" : "0"),
            new("stageId", stageId),
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };
        await db.HashSetAsync(lobbyKey, entries);

        // ホストをプレイヤーとして追加
        var playerData = JsonHelper.Serialize(new { playerName, isReady = false, joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        await db.HashSetAsync($"lobby:{lobbyId}:players", hostUserId, playerData);

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
        _logger.LogInformation("[DIAG] AddPlayerAsync request: lobbyId={LobbyId}, userId={UserId}, playerName={PlayerName}",
            lobbyId, userId, playerName);

        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            var db = _redis.GetDatabase();

            // ロビー存在チェック
            var exists = await db.KeyExistsAsync($"lobby:{lobbyId}");
            if (!exists)
            {
                _logger.LogWarning("[DIAG] AddPlayerAsync rejected: lobby {LobbyId} does not exist (userId={UserId})",
                    lobbyId, userId);
                return false;
            }

            // 最大人数チェック
            var maxPlayersValue = await db.HashGetAsync($"lobby:{lobbyId}", "maxPlayers");
            if (!maxPlayersValue.HasValue)
            {
                _logger.LogWarning("[DIAG] AddPlayerAsync rejected: maxPlayers field missing for lobby {LobbyId} (userId={UserId})",
                    lobbyId, userId);
                return false;
            }

            var maxPlayers = maxPlayersValue.ToInt();
            var currentCount = await db.HashLengthAsync($"lobby:{lobbyId}:players");
            if (currentCount >= maxPlayers)
            {
                _logger.LogWarning("[DIAG] AddPlayerAsync rejected: lobby {LobbyId} is full ({Current}/{Max}, userId={UserId})",
                    lobbyId, currentCount, maxPlayers, userId);
                return false;
            }

            // 多重参加防止
            var currentLobby = await db.StringGetAsync($"lobby:player:{userId}");
            if (currentLobby.HasValue)
            {
                _logger.LogWarning("[DIAG] AddPlayerAsync rejected: userId={UserId} already in lobby {OtherLobbyId} (target={LobbyId})",
                    userId, (string)currentLobby, lobbyId);
                return false;
            }

            var playerData = JsonHelper.Serialize(new { playerName, isReady = false, joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            await db.HashSetAsync($"lobby:{lobbyId}:players", userId, playerData);
            await db.StringSetAsync($"lobby:player:{userId}", lobbyId);

            _logger.LogInformation("[DIAG] AddPlayerAsync success: userId={UserId} added to lobby {LobbyId} (count={Count}/{Max})",
                userId, lobbyId, currentCount + 1, maxPlayers);
            return true;
        }
    }

    public async Task<bool> RemovePlayerAsync(string lobbyId, string userId)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            var db = _redis.GetDatabase();

            var removed = await db.HashDeleteAsync($"lobby:{lobbyId}:players", userId);
            if (!removed) return false;

            await db.KeyDeleteAsync($"lobby:player:{userId}");

            // プレイヤーがいなくなったらロビーを削除
            var remainingPlayers = await db.HashLengthAsync($"lobby:{lobbyId}:players");
            if (remainingPlayers == 0)
            {
                await DeleteCoreAsync(lobbyId);
            }

            _logger.LogDebug("Player {UserId} removed from lobby {LobbyId}", userId, lobbyId);
            return true;
        }
    }

    public async Task<LobbyInfo?> GetLobbyAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();

        var batch = db.CreateBatch();
        var hashTask = batch.HashGetAllAsync($"lobby:{lobbyId}");
        var countTask = batch.HashLengthAsync($"lobby:{lobbyId}:players");
        batch.Execute();

        var hash = await hashTask;
        if (hash.Length == 0) return null;

        var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
        var playerCount = await countTask;

        return new LobbyInfo
        {
            LobbyId = lobbyId,
            LobbyName = dict.GetString("name"),
            HostUserId = dict.GetString("hostUserId"),
            GameMode = dict.GetString("gameMode"),
            CurrentPlayers = checked((int)playerCount),
            MaxPlayers = dict.GetInt("maxPlayers", 4),
            IsPublic = dict.GetBool("isPublic"),
            StageId = dict.GetInt("stageId", 1),
        };
    }

    public async Task<LobbyPlayerInfo[]> GetPlayersAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();

        var batch = db.CreateBatch();
        var hashTask = batch.HashGetAllAsync($"lobby:{lobbyId}:players");
        var hostTask = batch.HashGetAsync($"lobby:{lobbyId}", "hostUserId");
        batch.Execute();

        var hash = await hashTask;
        var hostUserId = (string?)await hostTask ?? "";

        var players = new LobbyPlayerInfo[hash.Length];
        for (var i = 0; i < hash.Length; i++)
        {
            var userId = hash[i].Name.ToString();
            var data = JsonHelper.TryDeserialize<PlayerData>(hash[i].Value.ToString(), _logger, "player data");
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
        var allLobbyIds = await db.SetMembersAsync($"lobby:public:{gameMode}");
        if (allLobbyIds.Length == 0) return Array.Empty<LobbyInfo>();

        // 満員ロビー除外バッファとして maxResults の2倍までに制限
        var lobbyIds = allLobbyIds.Length > maxResults * 2
            ? allLobbyIds.AsSpan(0, maxResults * 2).ToArray()
            : allLobbyIds;

        var batch = db.CreateBatch();
        var hashTasks = lobbyIds.Select(id => batch.HashGetAllAsync($"lobby:{id}")).ToArray();
        var countTasks = lobbyIds.Select(id => batch.HashLengthAsync($"lobby:{id}:players")).ToArray();
        batch.Execute();

        var results = new List<LobbyInfo>();
        for (var i = 0; i < lobbyIds.Length; i++)
        {
            if (results.Count >= maxResults) break;

            var hash = await hashTasks[i];
            if (hash.Length == 0) continue;

            var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
            var playerCount = checked((int)await countTasks[i]);
            var mp = dict.GetInt("maxPlayers", 4);

            if (playerCount < mp)
            {
                results.Add(new LobbyInfo
                {
                    LobbyId = lobbyIds[i].ToString(),
                    LobbyName = dict.GetString("name"),
                    HostUserId = dict.GetString("hostUserId"),
                    GameMode = dict.GetString("gameMode"),
                    CurrentPlayers = playerCount,
                    MaxPlayers = mp,
                    IsPublic = dict.GetBool("isPublic"),
                    StageId = dict.GetInt("stageId", 1),
                });
            }
        }

        return results.ToArray();
    }

    public async Task<bool> SetReadyAsync(string lobbyId, string userId, bool isReady)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            var db = _redis.GetDatabase();
            var raw = await db.HashGetAsync($"lobby:{lobbyId}:players", userId);
            if (!raw.HasValue) return false;

            var data = JsonHelper.TryDeserialize<PlayerData>(raw.ToString(), _logger, "player data");
            if (data == null) return false;

            data.isReady = isReady;
            await db.HashSetAsync($"lobby:{lobbyId}:players", userId, JsonHelper.Serialize(data));
            return true;
        }
    }

    public async Task<(bool Success, bool AllReady)> SetReadyAndCheckAllAsync(string lobbyId, string userId, bool isReady)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            var db = _redis.GetDatabase();

            // SetReady
            var raw = await db.HashGetAsync($"lobby:{lobbyId}:players", userId);
            if (!raw.HasValue) return (false, false);

            var data = JsonHelper.TryDeserialize<PlayerData>(raw.ToString(), _logger, "player data");
            if (data == null) return (false, false);

            data.isReady = isReady;
            await db.HashSetAsync($"lobby:{lobbyId}:players", userId, JsonHelper.Serialize(data));

            // AreAllReady チェック（同じロック内で実行 → アトミック）
            var hash = await db.HashGetAllAsync($"lobby:{lobbyId}:players");
            var allReady = hash.Length > 0;
            foreach (var entry in hash)
            {
                var playerData = JsonHelper.TryDeserialize<PlayerData>(entry.Value.ToString(), _logger, "player data");
                if (playerData is not { isReady: true })
                {
                    allReady = false;
                    break;
                }
            }

            return (true, allReady);
        }
    }

    public async Task<bool> AreAllReadyAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"lobby:{lobbyId}:players");

        foreach (var entry in hash)
        {
            var data = JsonHelper.TryDeserialize<PlayerData>(entry.Value.ToString(), _logger, "player data");
            if (data is not { isReady: true }) return false;
        }

        return hash.Length > 0;
    }

    /// <summary>
    /// ロビー内全プレイヤーの Ready 状態を false にリセットする。
    /// ゲーム開始時に呼び出して、リザルト後に LobbyRoomScene へ戻った際に Ready が残っているのを防ぐ。
    /// </summary>
    public async Task ResetAllReadyAsync(string lobbyId)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            var db = _redis.GetDatabase();
            var hash = await db.HashGetAllAsync($"lobby:{lobbyId}:players");

            foreach (var entry in hash)
            {
                var data = JsonHelper.TryDeserialize<PlayerData>(entry.Value.ToString(), _logger, "player data");
                if (data == null || !data.isReady) continue;

                data.isReady = false;
                await db.HashSetAsync($"lobby:{lobbyId}:players", entry.Name, JsonHelper.Serialize(data));
            }
        }
    }

    public async Task SetStageAsync(string lobbyId, int stageId)
    {
        var db = _redis.GetDatabase();
        await db.HashSetAsync($"lobby:{lobbyId}", "stageId", stageId);
    }

    public async Task DeleteAsync(string lobbyId)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:lobby:{lobbyId}"))
        {
            await DeleteCoreAsync(lobbyId);
        }
    }

    /// <summary>
    /// ロビー削除の内部実装（ロック保持前提）
    /// RemovePlayerAsync から呼ばれる場合は既にロック保持済みのため、直接呼び出す
    /// </summary>
    private async Task DeleteCoreAsync(string lobbyId)
    {
        var db = _redis.GetDatabase();

        // Phase 1: メタデータ取得（gameMode + players を1バッチ）
        var readBatch = db.CreateBatch();
        var gameModeTask = readBatch.HashGetAsync($"lobby:{lobbyId}", "gameMode");
        var playersTask = readBatch.HashGetAllAsync($"lobby:{lobbyId}:players");
        readBatch.Execute();

        var gameMode = (string?)await gameModeTask;
        var playerEntries = await playersTask;

        // Phase 2: 全削除を1バッチ
        var deleteBatch = db.CreateBatch();
        var deleteTasks = new List<Task>();

        foreach (var entry in playerEntries)
        {
            deleteTasks.Add(deleteBatch.KeyDeleteAsync($"lobby:player:{entry.Name}"));
        }

        deleteTasks.Add(deleteBatch.KeyDeleteAsync($"lobby:{lobbyId}"));
        deleteTasks.Add(deleteBatch.KeyDeleteAsync($"lobby:{lobbyId}:players"));

        if (!string.IsNullOrEmpty(gameMode))
        {
            deleteTasks.Add(deleteBatch.SetRemoveAsync($"lobby:public:{gameMode}", lobbyId));
        }

        deleteBatch.Execute();
        await Task.WhenAll(deleteTasks);

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
