using System;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 起動時の設定値を保持する不変 POCO。
    /// <see cref="UnityServerConfigFactory.BuildAsync"/> で構築される。
    /// </summary>
    public sealed class UnityServerConfig
    {
        /// <summary>この DS の一意識別子（起動時に自動生成）。</summary>
        public string DsId { get; }

        /// <summary>Game.Server の URL（自己登録・ハートビートに使用）。未設定時は null。</summary>
        public string GameServerUrl { get; }

        /// <summary>Fusion UDP ポート番号。デフォルト 7777。</summary>
        public ushort GamePort { get; }

        /// <summary>ヘルスチェック TCP ポート番号。デフォルト 7778。</summary>
        public int HealthPort { get; }

        /// <summary>
        /// HMAC 認証用シークレットキー。
        /// 未設定時は <see cref="ReadOnlyMemory{T}.IsEmpty"/> が true。
        /// </summary>
        public ReadOnlyMemory<byte> AuthSecretKey { get; }

        /// <summary>DS の公開アドレス（GCE 外部 IP または手動設定）。クライアント UDP 接続用。未設定時は null。</summary>
        public string PublicAddress { get; }

        /// <summary>
        /// DS の VPC 内部 IP アドレス（GCE 内部 IP または手動設定）。
        /// Game.Server → DS 間の HTTP 通信（VPC Connector 経由）に使用する。未設定時は null。
        /// </summary>
        public string InternalAddress { get; }

        /// <summary>
        /// <see cref="UnityServerConfig"/> を初期化する。
        /// </summary>
        /// <param name="dsId">DS 識別子。</param>
        /// <param name="gameServerUrl">Game.Server URL（null 可）。</param>
        /// <param name="gamePort">Fusion UDP ポート番号。</param>
        /// <param name="healthPort">ヘルスチェックポート番号。</param>
        /// <param name="authSecretKey">HMAC シークレット（空の場合は認証スキップ）。</param>
        /// <param name="publicAddress">公開アドレス（null 可）。</param>
        /// <param name="internalAddress">VPC 内部 IP アドレス（null 可）。</param>
        public UnityServerConfig(
            string dsId,
            string gameServerUrl,
            ushort gamePort,
            int healthPort,
            ReadOnlyMemory<byte> authSecretKey,
            string publicAddress,
            string internalAddress = null)
        {
            DsId = dsId;
            GameServerUrl = gameServerUrl;
            GamePort = gamePort;
            HealthPort = healthPort;
            AuthSecretKey = authSecretKey;
            PublicAddress = publicAddress;
            InternalAddress = internalAddress;
        }
    }
}
