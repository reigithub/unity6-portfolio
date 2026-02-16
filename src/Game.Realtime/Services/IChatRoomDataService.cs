using Game.Library.Shared.Realtime.Dto;

namespace Game.Realtime.Services;

/// <summary>
/// チャットルーム Valkey データ管理サービスインターフェース
/// 権限チェックは行わない（サーバー内部 API）
/// </summary>
public interface IChatRoomDataService
{
    Task<string> CreateAsync(string roomName, string roomType, int maxMembers, int defaultPermissions);
    Task<bool> ExistsAsync(string roomId);
    Task<bool> AddMemberAsync(string roomId, string userId, string playerName, int permissions);
    Task<bool> RemoveMemberAsync(string roomId, string userId);
    Task<ChatRoomInfo?> GetRoomAsync(string roomId);
    Task<ChatRoomMemberInfo[]> GetMembersAsync(string roomId);
    Task<int> GetMemberPermissionsAsync(string roomId, string userId);
    Task<bool> SetMemberPermissionsAsync(string roomId, string userId, int permissions);
    Task<int> GetDefaultPermissionsAsync(string roomId);
    Task DeleteAsync(string roomId);
}
