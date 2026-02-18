using Game.Library.Shared.Dto;

namespace Game.Server.Hubs;

/// <summary>
/// チャット SignalR Hub サーバー → クライアント通知インターフェース（強い型付け）
/// 1接続で複数ルームに参加するため、すべてのコールバックに roomId を含める
/// </summary>
public interface IChatHubClient
{
    Task OnMessageReceived(string roomId, ChatMessage message);
    Task OnPlayerJoined(string roomId, string userId, string playerName);
    Task OnPlayerLeft(string roomId, string userId, string playerName);
    Task OnRoomDeleted(string roomId, string reason);
    Task OnPermissionsChanged(string roomId, int permissions);
}
