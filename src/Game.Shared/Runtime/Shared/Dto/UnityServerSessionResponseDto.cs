using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// DS からのセッション作成レスポンス DTO。
    /// DS の <c>POST /session/start</c> レスポンスとして返される。
    /// </summary>
    [MessagePackObject]
    public class UnityServerSessionResponse
    {
        /// <summary>
        /// マッチID（リクエストと同一）。
        /// </summary>
        [Key(0)]
        public string MatchId { get; set; } = string.Empty;

        /// <summary>
        /// Fusion セッション名（クライアントが接続に使用）。
        /// </summary>
        [Key(1)]
        public string SessionName { get; set; } = string.Empty;

        /// <summary>
        /// セッション作成に成功したかどうか。
        /// </summary>
        [Key(2)]
        public bool Success { get; set; }

        /// <summary>
        /// エラーメッセージ。<see cref="Success"/> が false の場合に設定される。
        /// </summary>
        [Key(3)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
