using Game.Library.Shared.Enums;
using Game.Server.Services.Chat.Exceptions;

namespace Game.Server.Services.Chat;

/// <summary>
/// チャットルーム権限検証ヘルパー
/// Controller と Hub の両方から利用する
/// </summary>
public class ChatPermissionValidator
{
    private readonly IChatRoomDataService _roomDataService;

    public ChatPermissionValidator(IChatRoomDataService roomDataService)
    {
        _roomDataService = roomDataService;
    }

    /// <summary>
    /// 指定ユーザーが必要権限を持っているか検証する。
    /// 持っていない場合は ChatPermissionException をスローする。
    /// </summary>
    public async Task ValidateAsync(string roomId, string userId, ChatRoomPermissions required)
    {
        var permissions = await _roomDataService.GetMemberPermissionsAsync(roomId, userId);
        if (((ChatRoomPermissions)permissions & required) != required)
        {
            throw new ChatPermissionException($"Missing permission: {required}");
        }
    }

    /// <summary>
    /// 指定ユーザーが必要権限を持っているかを bool で返す（例外なし）。
    /// </summary>
    public async Task<bool> HasPermissionAsync(string roomId, string userId, ChatRoomPermissions required)
    {
        var permissions = await _roomDataService.GetMemberPermissionsAsync(roomId, userId);
        return ((ChatRoomPermissions)permissions & required) == required;
    }

    /// <summary>
    /// ルームの存在を確認する。存在しない場合は ChatNotFoundException をスローする。
    /// </summary>
    public async Task ValidateRoomExistsAsync(string roomId)
    {
        if (!await _roomDataService.ExistsAsync(roomId))
        {
            throw new ChatNotFoundException("Chat room not found");
        }
    }

    /// <summary>
    /// ルームのデフォルト権限に指定権限が含まれているか確認する。
    /// </summary>
    public async Task<bool> HasDefaultPermissionAsync(string roomId, ChatRoomPermissions required)
    {
        var defaultPermissions = await _roomDataService.GetDefaultPermissionsAsync(roomId);
        return ((ChatRoomPermissions)defaultPermissions & required) == required;
    }
}
