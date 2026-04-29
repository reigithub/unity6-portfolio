using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// Dedicated Server ハートビートリクエスト DTO。
    /// DS が 30 秒間隔で Game.Server の <c>POST /api/unity-server/heartbeat</c> へ送信する。
    /// </summary>
    [MessagePackObject]
    public class UnityServerHeartbeatRequest
    {
        /// <summary>
        /// ハートビートを送信する DS の識別子。
        /// </summary>
        [Key(0)]
        public string DsId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dedicated Server 登録解除リクエスト DTO。
    /// DS 正常終了時に Game.Server の <c>POST /api/unity-server/deregister</c> へ送信する。
    /// </summary>
    [MessagePackObject]
    public class UnityServerDeregisterRequest
    {
        /// <summary>
        /// 登録解除する DS の識別子。
        /// </summary>
        [Key(0)]
        public string DsId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dedicated Server セッション終了通知リクエスト DTO。
    /// DS がセッション完了後に Game.Server の <c>POST /api/unity-server/session-ended</c> へ送信する。
    /// </summary>
    [MessagePackObject]
    public class UnityServerSessionEndedRequest
    {
        /// <summary>
        /// セッションが終了した DS の識別子。
        /// </summary>
        [Key(0)]
        public string DsId { get; set; } = string.Empty;

        /// <summary>
        /// 終了した Fusion セッション名（SessionName）。
        /// </summary>
        [Key(1)]
        public string SessionName { get; set; } = string.Empty;
    }
}
