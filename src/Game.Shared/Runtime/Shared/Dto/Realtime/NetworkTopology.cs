namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// ロビーセッションのネットワークトポロジ。
    /// ロビー作成時にホストが選択し、StartGameAsync で接続経路の分岐に使用される。
    ///
    /// NOTE: 本来は lobby 責務 (matching/chat/ready) と分離すべき概念だが、
    /// auto-Ready-start アーキテクチャ変更コストとのトレードオフで例外的に lobby data に含めている。
    /// 後続 PR で client-driven 化 (SetReadyAsync 拡張 or explicit Start RPC) による分離を検討。
    /// </summary>
    public enum NetworkTopology
    {
        /// <summary>クライアント-サーバー型 (Dedicated Server 経由、既定)。</summary>
        DedicatedServer = 0,

        /// <summary>ピアツーピア型 (Photon Cloud 経由 Host モード)。</summary>
        PeerToPeer = 1,
    }
}
