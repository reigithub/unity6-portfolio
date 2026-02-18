using System.Text.Json;
using Game.Library.Shared.Chat.Dto;
using Medallion.Threading;
using StackExchange.Redis;

namespace Game.Server.Services.Chat;

/// <summary>
/// Valkey ベースのチャットルームデータ管理サービス
/// Hash でルームメタデータ・メンバー情報を管理する
/// </summary>
public class ChatRoomDataService : IChatRoomDataService
{
    private const string RoomKeyPrefix = "chatroom:";
    private const string MembersSuffix = ":members";

    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<ChatRoomDataService> _logger;

    public ChatRoomDataService(
        IConnectionMultiplexer redis,
        IDistributedLockProvider lockProvider,
        ILogger<ChatRoomDataService> logger)
    {
        _redis = redis;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    public async Task<string> CreateAsync(string roomName, string roomType, int maxMembers, int defaultPermissions)
    {
        var db = _redis.GetDatabase();
        var roomId = Guid.NewGuid().ToString("N");

        var roomKey = $"{RoomKeyPrefix}{roomId}";
        var entries = new HashEntry[]
        {
            new("name", roomName),
            new("roomType", roomType),
            new("maxMembers", maxMembers),
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new("defaultPermissions", defaultPermissions),
        };
        await db.HashSetAsync(roomKey, entries);

        _logger.LogInformation(
            "Chat room {RoomId} created (name: {RoomName}, type: {RoomType})",
            roomId, roomName, roomType);

        return roomId;
    }

    public async Task<bool> ExistsAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"{RoomKeyPrefix}{roomId}");
    }

    public async Task<bool> AddMemberAsync(string roomId, string userId, string playerName, int permissions)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:chatroom:{roomId}"))
        {
            var db = _redis.GetDatabase();
            var roomKey = $"{RoomKeyPrefix}{roomId}";

            if (!await db.KeyExistsAsync(roomKey))
                return false;

            var maxMembers = (int)await db.HashGetAsync(roomKey, "maxMembers");
            if (maxMembers > 0)
            {
                var currentCount = await db.HashLengthAsync($"{roomKey}{MembersSuffix}");
                if (currentCount >= maxMembers)
                    return false;
            }

            var memberData = JsonSerializer.Serialize(new MemberData
            {
                playerName = playerName,
                joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                permissions = permissions,
            });
            await db.HashSetAsync($"{roomKey}{MembersSuffix}", userId, memberData);

            _logger.LogDebug("Member {UserId} added to chat room {RoomId}", userId, roomId);
            return true;
        }
    }

    public async Task<bool> RemoveMemberAsync(string roomId, string userId)
    {
        var db = _redis.GetDatabase();
        var removed = await db.HashDeleteAsync($"{RoomKeyPrefix}{roomId}{MembersSuffix}", userId);

        if (removed)
        {
            _logger.LogDebug("Member {UserId} removed from chat room {RoomId}", userId, roomId);
        }

        return removed;
    }

    public async Task<ChatRoomInfo?> GetRoomAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        var roomKey = $"{RoomKeyPrefix}{roomId}";
        var hash = await db.HashGetAllAsync(roomKey);
        if (hash.Length == 0) return null;

        var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
        var memberCount = await db.HashLengthAsync($"{roomKey}{MembersSuffix}");

        return new ChatRoomInfo
        {
            RoomId = roomId,
            RoomName = dict.GetValueOrDefault("name", ""),
            RoomType = dict.GetValueOrDefault("roomType", ""),
            CurrentMembers = (int)memberCount,
            MaxMembers = int.TryParse(dict.GetValueOrDefault("maxMembers", "0"), out var mm) ? mm : 0,
            CreatedAt = long.TryParse(dict.GetValueOrDefault("createdAt", "0"), out var ca) ? ca : 0,
            DefaultPermissions = int.TryParse(dict.GetValueOrDefault("defaultPermissions", "0"), out var dp) ? dp : 0,
        };
    }

    public async Task<ChatRoomMemberInfo[]> GetMembersAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"{RoomKeyPrefix}{roomId}{MembersSuffix}");

        var members = new ChatRoomMemberInfo[hash.Length];
        for (var i = 0; i < hash.Length; i++)
        {
            var userId = hash[i].Name.ToString();
            var data = JsonSerializer.Deserialize<MemberData>(hash[i].Value.ToString());
            members[i] = new ChatRoomMemberInfo
            {
                UserId = userId,
                PlayerName = data?.playerName ?? "",
                JoinedAt = data?.joinedAt ?? 0,
                Permissions = data?.permissions ?? 0,
            };
        }

        return members;
    }

    public async Task<int> GetMemberPermissionsAsync(string roomId, string userId)
    {
        var db = _redis.GetDatabase();
        var raw = await db.HashGetAsync($"{RoomKeyPrefix}{roomId}{MembersSuffix}", userId);
        if (!raw.HasValue) return 0;

        var data = JsonSerializer.Deserialize<MemberData>(raw.ToString());
        return data?.permissions ?? 0;
    }

    public async Task<bool> SetMemberPermissionsAsync(string roomId, string userId, int permissions)
    {
        await using (await _lockProvider.AcquireLockAsync($"lock:chatroom:{roomId}"))
        {
            var db = _redis.GetDatabase();
            var membersKey = $"{RoomKeyPrefix}{roomId}{MembersSuffix}";
            var raw = await db.HashGetAsync(membersKey, userId);
            if (!raw.HasValue) return false;

            var data = JsonSerializer.Deserialize<MemberData>(raw.ToString());
            if (data == null) return false;

            data.permissions = permissions;
            await db.HashSetAsync(membersKey, userId, JsonSerializer.Serialize(data));
            return true;
        }
    }

    public async Task<int> GetDefaultPermissionsAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        var value = await db.HashGetAsync($"{RoomKeyPrefix}{roomId}", "defaultPermissions");
        return int.TryParse(value, out var dp) ? dp : 0;
    }

    public async Task DeleteAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        var roomKey = $"{RoomKeyPrefix}{roomId}";

        await db.KeyDeleteAsync($"{roomKey}{MembersSuffix}");
        await db.KeyDeleteAsync(roomKey);

        _logger.LogInformation("Chat room {RoomId} deleted", roomId);
    }

    private class MemberData
    {
        public string playerName { get; set; } = "";
        public long joinedAt { get; set; }
        public int permissions { get; set; }
    }
}
