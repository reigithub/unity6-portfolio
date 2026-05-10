using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// LobbyHub.OnGameStarting / MatchmakingHub.OnMatchFound で server から client に送信されるゲームセッション開始情報。
    /// DS / P2P 両モード兼用 — <see cref="Topology"/> で実際に使用するフィールドが決まる。
    /// </summary>
    [MessagePackObject]
    public record GameSessionStartInfo
    {
        /// <summary>ネットワークトポロジ。受信側はこれで分岐する。</summary>
        [Key(0)]
        public NetworkTopology Topology { get; init; }

        /// <summary>セッション識別子</summary>
        [Key(1)]
        public string SessionName { get; init; } = string.Empty;

        /// <summary>サーバーアドレス</summary>
        [Key(2)]
        public string? ServerAddress { get; init; }

        /// <summary>サーバーポート</summary>
        [Key(3)]
        public int ServerPort { get; init; }

        /// <summary>セッショントークン</summary>
        [Key(4)]
        public string? SessionToken { get; init; }

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

        /// <summary>ステージ ID。Quick Match / Lobby 両経路でゲーム開始ステージを示す。</summary>
        [Key(8)]
        public int StageId { get; init; }
    }
}
