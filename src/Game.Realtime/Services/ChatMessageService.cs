using System.Text.Json;
using Game.Library.Shared.Realtime.Hubs;
using StackExchange.Redis;

namespace Game.Realtime.Services;

/// <summary>
/// Valkey ベースのチャットメッセージ永続化サービス
/// Sorted Set (score=timestamp) でルームごとのメッセージ履歴を管理する
/// </summary>
public class ChatMessageService : IChatMessageService
{
    private const string KeyPrefix = "chat:messages:";
    private const int MaxMessagesPerRoom = 200;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ChatMessageService> _logger;

    public ChatMessageService(IConnectionMultiplexer redis, ILogger<ChatMessageService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SaveMessageAsync(string roomId, ChatMessage message)
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

        // score = Timestamp（UnixTimeMilliseconds）で時系列ソート
        await db.SortedSetAddAsync(key, json, message.Timestamp);

        // 古いメッセージを削除してメモリを節約
        // 上位 MaxMessagesPerRoom 件だけ残す
        var length = await db.SortedSetLengthAsync(key);
        if (length > MaxMessagesPerRoom)
        {
            await db.SortedSetRemoveRangeByRankAsync(key, 0, length - MaxMessagesPerRoom - 1);
        }

        _logger.LogDebug(
            "Saved chat message from {UserId} in room {RoomId}",
            message.UserId, roomId);
    }

    public async Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{roomId}";

        // 最新 N 件を取得（スコア降順 = 新しい順）
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

    public async Task DeleteRoomAsync(string roomId)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{roomId}";
        await db.KeyDeleteAsync(key);

        _logger.LogInformation("Deleted chat messages for room {RoomId}", roomId);
    }

    private class ChatMessageData
    {
        public string userId { get; set; } = "";
        public string playerName { get; set; } = "";
        public string content { get; set; } = "";
        public long timestamp { get; set; }
    }
}
