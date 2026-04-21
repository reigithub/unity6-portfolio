using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// DS へのセッション作成リクエスト DTO。
    /// Game.Server が DS の <c>POST /session/start</c> へ送信する。
    /// </summary>
    [MessagePackObject]
    public class UnityServerSessionRequest
    {
        /// <summary>
        /// Fusion セッション名（SessionName）。セッション識別子として使用する。
        /// </summary>
        [Key(0)]
        public string SessionName { get; set; } = string.Empty;

        /// <summary>
        /// ステージID。
        /// </summary>
        [Key(1)]
        public int StageId { get; set; }

        /// <summary>
        /// このセッションの期待プレイヤー数。
        /// </summary>
        [Key(2)]
        public int ExpectedPlayers { get; set; }
    }
}
