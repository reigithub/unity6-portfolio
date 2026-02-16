using Game.Library.Shared.Realtime.Hubs;

namespace Game.Realtime.Services;

/// <summary>
/// チャットメッセージ永続化サービスインターフェース
/// Valkey Sorted Set でルームごとのメッセージ履歴を管理する
/// </summary>
public interface IChatMessageService
{
    /// <summary>
    /// メッセージを保存する（score = Timestamp）
    /// </summary>
    Task SaveMessageAsync(string roomId, ChatMessage message);

    /// <summary>
    /// 最新の N 件のメッセージを取得する（新しい順 → 古い順に返す）
    /// </summary>
    Task<ChatMessage[]> GetRecentMessagesAsync(string roomId, int count);

    /// <summary>
    /// ルームのメッセージ履歴を全て削除する
    /// </summary>
    Task DeleteRoomAsync(string roomId);
}
