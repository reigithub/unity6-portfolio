using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// LobbyHub.OnGameStarting で server から client に送信されるゲーム開始情報。
    /// DS / P2P 両モード兼用 — <see cref="Topology"/> で実際に使用するフィールドが決まる。
    ///
    /// NOTE: 既存 <see cref="MatchResult"/> (Quick Match 用) とはセマンティクス分離のため別 DTO。
    /// Quick Match 経路を将来統合する場合は MatchResult を MatchStartInfo に置換可能。
    /// </summary>
    [MessagePackObject]
    public record MatchStartInfo
    {
        /// <summary>ネットワークトポロジ。受信側はこれで分岐する。</summary>
        [Key(0)]
        public NetworkTopology Topology { get; init; }

        /// <summary>セッション識別子。DS: matchId、P2P: Photon SessionName。</summary>
        [Key(1)]
        public string SessionName { get; init; } = string.Empty;

        // --- DS 専用 (Topology == Dedicated 時に使用、P2P では null/0) ---

        /// <summary>DS サーバーアドレス (Topology == Dedicated 時に使用)。</summary>
        [Key(2)]
        public string? ServerAddress { get; init; }

        /// <summary>DS サーバーポート (Topology == Dedicated 時に使用)。</summary>
        [Key(3)]
        public int ServerPort { get; init; }

        /// <summary>DS セッショントークン (Topology == Dedicated 時に使用)。</summary>
        [Key(4)]
        public string? SessionToken { get; init; }

        // --- P2P 専用 (Topology == PeerToPeer 時に使用、DS では null) ---

        /// <summary>Photon Cloud リージョン</summary>
        [Key(5)]
        public string? PhotonRegion { get; init; }

        /// <summary>Photon Host を担当するプレイヤー UserId</summary>
        [Key(6)]
        public string? HostUserId { get; init; }

        /// <summary>
        /// セッション開始時の実プレイヤー数
        /// Ready 完了時の 実接続プレイヤー数を渡すこと
        /// </summary>
        [Key(7)]
        public int PlayerCount { get; init; }
    }
}
