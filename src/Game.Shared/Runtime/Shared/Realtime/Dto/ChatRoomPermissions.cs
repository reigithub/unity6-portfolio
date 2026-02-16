using System;

namespace Game.Library.Shared.Realtime.Dto
{
    /// <summary>
    /// チャットルーム権限ビットフラグ
    /// </summary>
    [Flags]
    public enum ChatRoomPermissions
    {
        None         = 0,
        Join         = 1 << 0,
        SendMessage  = 1 << 1,
        Leave        = 1 << 2,
        Invite       = 1 << 3,
        Kick         = 1 << 4,
        Delete       = 1 << 5,
        ManageMember = 1 << 6,
        ManageRoom   = 1 << 7,
    }
}
