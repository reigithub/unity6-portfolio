using Fusion;
using Fusion.Sockets;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion セッション開始に必要な接続パラメータをまとめた構造体。
    /// SurvivorFusionStageConnector が生成し、SurvivorFusionRunner に渡す。
    /// </summary>
    public struct FusionConnectionConfig
    {
        /// <summary>ゲームモード（Host / Client / Server）</summary>
        public GameMode GameMode;

        /// <summary>セッション名（マッチ ID と一致させる）</summary>
        public string SessionName;

        /// <summary>バインドアドレス（Server: NetAddress.Any(port)、Client: NetAddress.Any()）</summary>
        public NetAddress Address;

        /// <summary>NAT 越え用の公開アドレス。不要な場合は null。</summary>
        public NetAddress? CustomPublicAddress;

        /// <summary>
        /// Fusion ConnectionToken（128 バイト以内）。
        /// Client: セッショントークンの UTF-8 バイト列。Server: null。
        /// </summary>
        public byte[] ConnectionToken;

        /// <summary>
        /// Photon Cloud のリージョン識別子 (P2P 用、例: "jp" / "us" / "eu")。
        /// null/空 の場合は <c>PhotonAppSettings.Instance.AppSettings.FixedRegion</c> にフォールバック。
        /// SurvivorFusionRunner.StartAsync が non-null 時に AppSettings.GetCopy() + FixedRegion 上書きを実施。
        /// </summary>
        public string PhotonRegion;
    }
}
