using System.Text.Json;
using Game.Library.Shared.Dto;
using Game.Server.Configuration;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Game.Server.Services.Chat;

/// <summary>
/// Valkey ベースのチャットメッセージ永続化サービス
/// Sorted Set (score=timestamp) でルームごとのメッセージ履歴を管理する
/// </summary>
public class ChatMessageService : IChatMessageService
{
    private const string KeyPrefix = "chat:messages:";

    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly int _maxMessagesPerRoom;
    private readonly ILogger<ChatMessageService> _logger;

    public ChatMessageService(
        IConnectionMultiplexer redis,
        IDistributedLockProvider lockProvider,
        IOptions<ChatSettings> chatSettings,
        ILogger<ChatMessageService> logger)
    {
        _redis = redis;
        _lockProvider = lockProvider;
        _maxMessagesPerRoom = chatSettings.Value.MaxMessagesPerRoom;
        _logger = logger;
    }

    public async Task SaveMessageAsync(string roomId, ChatMessage message)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{roomId}";

            var json = JsonSerializer.Serialize(new ChatMessageData
            {
                userId = message.UserId,
                playerName = message.PlayerName,
                content = message.Content,
                timestamp = message.Timestamp,
            });

            await using (await _lockProvider.AcquireLockAsync($"lock:chat:messages:{roomId}"))
            {
                await db.SortedSetAddAsync(key, json, message.Timestamp);

                var length = await db.SortedSetLengthAsync(key);
                if (length > _maxMessagesPerRoom)
                {
                    await db.SortedSetRemoveRangeByRankAsync(key, 0, length - _maxMessagesPerRoom - 1);
                }
            }

            _logger.LogDebug(
                "Saved chat message from {UserId} in room {RoomId}",
                message.UserId, roomId);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, could not save message for roomId={RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chat message for roomId={RoomId}", roomId);
        }
    }

    public async Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{roomId}";

            var entries = await db.SortedSetRangeByRankAsync(
                key, -count, -1, Order.Ascending);

            var messages = new ChatMessage[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var data = JsonSerializer.Deserialize<ChatMessageData>(entries[i].ToString());
                messages[i] = new ChatMessage
                {
                    UserId = data?.userId ?? "",
                    PlayerName = data?.playerName ?? "",
                    Content = data?.content ?? "",
                    Timestamp = data?.timestamp ?? 0,
                };
            }

            return messages;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, returning empty messages for roomId={RoomId}", roomId);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent messages for roomId={RoomId}", roomId);
            return [];
        }
    }

    public async Task DeleteRoomAsync(string roomId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{roomId}";
            await db.KeyDeleteAsync(key);

            _logger.LogInformation("Deleted chat messages for room {RoomId}", roomId);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed, could not delete room data for roomId={RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chat room data for roomId={RoomId}", roomId);
        }
    }

    private class ChatMessageData
    {
        public string userId { get; set; } = "";
        public string playerName { get; set; } = "";
        public string content { get; set; } = "";
        public long timestamp { get; set; }
    }
}
