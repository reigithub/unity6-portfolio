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
        /// マッチID（セッション識別子）。
        /// </summary>
        [Key(0)]
        public string MatchId { get; set; } = string.Empty;

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
