using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// ネットワーク接続の接続元を表す列挙型。
    /// </summary>
    public enum ConnectionSource
    {
        /// <summary>未設定（初期値）。</summary>
        None,

        /// <summary>SP ローカルサーバー接続。</summary>
        Local,

        /// <summary>SP クラウドサーバー接続。</summary>
        Remote,

        /// <summary>MP マッチメイキング経由接続。</summary>
        Matchmaking,

        /// <summary>Dedicated Server 自身のバインド設定。</summary>
        DedicatedServer,

        /// <summary>P2P 自身がホスト (GameMode.Host) として起動。</summary>
        P2PHost,

        /// <summary>P2P 他人のホストにクライアント (GameMode.Client) として接続。</summary>
        P2PClient,
    }

    /// <summary>
    /// <see cref="IUnityServerSessionConfig"/> の実装。
    /// VContainer で Singleton 登録して使用する。
    /// </summary>
    public class UnityServerSessionConfig : IUnityServerSessionConfig
    {
        /// <summary>SP ローカルセッションのデフォルト名。</summary>
        private const string DefaultLocalSessionName = "sp-local";

        /// <summary>SP リモートセッションのデフォルト名。</summary>
        private const string DefaultRemoteSessionName = "sp-remote";

        /// <summary>ローカルホストアドレス。</summary>
        private const string DefaultLocalAddress = "127.0.0.1";

        /// <summary>デフォルトポート番号。</summary>
        private const ushort DefaultPort = 7777;

        /// <summary>現在の接続元種別。</summary>
        public ConnectionSource ConnectionSource { get; private set; }

        /// <summary>接続先サーバーアドレス。</summary>
        public string ServerAddress { get; private set; }

        /// <summary>接続先ポート番号。</summary>
        public ushort ServerPort { get; private set; }

        /// <summary>セッション名（Fusion セッション識別子）。</summary>
        public string SessionName { get; private set; }

        /// <summary>セッショントークン（HMAC 認証用）。</summary>
        public string SessionToken { get; private set; }

        /// <summary>
        /// ゲーム開始時点の実接続プレイヤー数
        /// タイトル画面のモード選択時またはロビー開始時にセットされる。
        /// </summary>
        public int PlayerCount { get; private set; } = 1;

        /// <summary>
        /// ロビーホストの UserId
        /// </summary>
        public string HostUserId { get; private set; } = string.Empty;

        /// <summary>
        /// クライアント接続経路（Local / Remote / Matchmaking / P2PClient）が設定済みかどうかを返す。
        /// SurvivorStageConnectScene の Phase 2 判定で使用する。
        /// P2PHost は Host モード起動 (= IsHostMode 経路) のためここには含めない。
        /// </summary>
        public bool IsClientConfigured => ConnectionSource is ConnectionSource.Local
                             or ConnectionSource.Remote
                             or ConnectionSource.Matchmaking
                             or ConnectionSource.P2PClient;

        /// <summary>
        /// Configure 後の最後の接続種別
        /// </summary>
        public ConnectionSource LastConnectionSource { get; private set; } = ConnectionSource.None;

        /// <summary>
        /// Photon Cloud のリージョン識別子 (P2P 用、例: "jp", "us", "eu")。
        /// null の場合は <c>PhotonAppSettings.asset</c> の <c>FixedRegion</c> にフォールバック。
        /// 本フィールドは PR1 で先行追加 (dormant)、PR3 で StartHostAsync が参照する。
        /// </summary>
        public string PhotonRegion { get; private set; }

        /// <summary>
        /// 全パラメータを初期化する。未指定はデフォルト値で補完。
        /// </summary>
        /// <param name="source">接続元の種別。</param>
        /// <param name="address">接続先アドレス。null 時は <see cref="DefaultLocalAddress"/>。</param>
        /// <param name="port">接続先ポート番号。null 時は <see cref="DefaultPort"/>。</param>
        /// <param name="sessionName">セッション名。null 時は <see cref="DefaultLocalSessionName"/>。</param>
        /// <param name="sessionToken">セッショントークン。null 時は空文字。</param>
        /// <param name="playerCount">ゲーム開始時点の実接続プレイヤー数 (全滅判定の分母)。</param>
        public void Configure(ConnectionSource source, string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null)
        {
            ConnectionSource = source;
            ServerAddress = address ?? DefaultLocalAddress;
            ServerPort = port ?? DefaultPort;
            SessionName = sessionName ?? (source is ConnectionSource.Remote ? DefaultRemoteSessionName : DefaultLocalSessionName);
            SessionToken = sessionToken ?? string.Empty;
            PlayerCount = playerCount ?? 1;
            PhotonRegion = null;  // PhotonRegion は MatchStartInfo overload 経由でのみ更新するため、対称性のため reset
            if (source != ConnectionSource.None) LastConnectionSource = source;
        }

        /// <summary>
        /// マッチメイキング結果から全パラメータを一括設定する。
        /// </summary>
        /// <param name="source">接続元の種別。</param>
        /// <param name="result">マッチメイキングサーバーから受け取った <see cref="MatchResult"/>。</param>
        /// <param name="playerCount">ゲーム開始時点の実接続プレイヤー数 (全滅判定の分母)。</param>
        public void Configure(ConnectionSource source, MatchResult result, int playerCount)
            => Configure(source, result.ServerAddress, (ushort)result.ServerPort, result.MatchId, result.SessionToken, playerCount);

        /// <summary>
        /// MatchStartInfo (DS / P2P 両用) から全パラメータを一括設定する。
        /// LobbyHub.OnGameStarting 経由のゲーム開始フローで使用する。
        /// </summary>
        /// <param name="source">接続元の種別 (P2PHost / P2PClient / Matchmaking 等、呼出側で host/client 判定して指定)。</param>
        /// <param name="info">サーバーから受信した <see cref="MatchStartInfo"/>。</param>
        /// <param name="playerCount">ゲーム開始時点の実接続プレイヤー数 (全滅判定の分母)。<see cref="MatchStartInfo.PlayerCount"/> から取得すること。</param>
        public void Configure(ConnectionSource source, MatchStartInfo info, int playerCount)
        {
            ConnectionSource = source;
            SessionName = info.SessionName;
            PlayerCount = playerCount;
            HostUserId = info.HostUserId;
            if (source != ConnectionSource.None) LastConnectionSource = source;

            if (info.Topology == NetworkTopology.PeerToPeer)
            {
                // P2P: ServerAddress/Port/Token は不使用、PhotonRegion を使用
                ServerAddress = null;
                ServerPort = 0;
                SessionToken = string.Empty;
                PhotonRegion = info.PhotonRegion;
            }
            else
            {
                // Dedicated: ServerAddress/Port/Token を populate、PhotonRegion は null
                ServerAddress = info.ServerAddress;
                ServerPort = (ushort)info.ServerPort;
                SessionToken = info.SessionToken;
                PhotonRegion = null;
            }
        }

        /// <summary>
        /// 指定パラメータのみ上書きする。null は既存値を維持。
        /// Dedicated Server のセッション開始時に sessionName のみ更新する用途で使用する。
        /// NOTE: PhotonRegion は本メソッドでは更新しない (Configure(source, MatchStartInfo, playerCount) overload 経由でのみ更新)。
        /// </summary>
        /// <param name="address">接続先アドレス。null で既存値を維持。</param>
        /// <param name="port">接続先ポート番号。null で既存値を維持。</param>
        /// <param name="sessionName">セッション名。null で既存値を維持。</param>
        /// <param name="sessionToken">セッショントークン。null で既存値を維持。</param>
        /// <param name="playerCount">期待プレイヤー数。</param>
        /// <param name="hostUserId">ロビーホストの UserId。null で既存値を維持。DS Server 側で手動ポーズ操作の権限判定に使用。</param>
        public void UpdateConfigure(string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null, string hostUserId = null)
        {
            if (address != null) ServerAddress = address;
            if (port.HasValue) ServerPort = port.Value;
            if (sessionName != null) SessionName = sessionName;
            if (sessionToken != null) SessionToken = sessionToken;
            if (playerCount.HasValue) PlayerCount = playerCount.Value;
            if (hostUserId != null) HostUserId = hostUserId;
        }

        /// <summary>
        /// 接続パラメータと期待プレイヤー数をリセットする。
        /// LastConnectionSource は履歴目的のため意図的に保持する (リザルト画面等で参照する)。
        /// PhotonRegion は「次回接続用設定値」のため reset する (SP/DS モード切替時に古い値が残らないように)。
        /// </summary>
        public void Clear()
        {
            ConnectionSource = ConnectionSource.None;
            ServerAddress = null;
            ServerPort = 0;
            SessionName = null;
            SessionToken = null;
            PlayerCount = 1;
            HostUserId = string.Empty;
            PhotonRegion = null;
        }

        public bool IsLocalAddress(string address)
            => string.IsNullOrEmpty(address) || address == "localhost" || address == DefaultLocalAddress;

        public bool IsHostUserId(string userId)
        {
            if (PlayerCount == 1) return true;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(HostUserId)) return false;
            return userId == HostUserId;
        }
    }
}
