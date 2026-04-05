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
    }

    /// <summary>
    /// ネットワーク接続に必要なパラメータをまとめた値型構造体。
    /// </summary>
    public readonly struct ConnectionParams
    {
        /// <summary>この接続パラメータの設定元。</summary>
        public readonly ConnectionSource Source;

        /// <summary>接続先アドレス。</summary>
        public readonly string Address;

        /// <summary>接続先ポート番号。</summary>
        public readonly ushort Port;

        /// <summary>セッション名（Fusion セッション識別子）。</summary>
        public readonly string SessionName;

        /// <summary>セッショントークン（HMAC 認証用）。</summary>
        public readonly string SessionToken;

        /// <summary>
        /// ConnectionParams を初期化する。
        /// </summary>
        /// <param name="source">接続元の種別。</param>
        /// <param name="address">接続先アドレス。</param>
        /// <param name="port">接続先ポート番号。</param>
        /// <param name="sessionName">セッション名。</param>
        /// <param name="sessionToken">セッショントークン。</param>
        public ConnectionParams(ConnectionSource source, string address, ushort port, string sessionName, string sessionToken)
        {
            Source = source;
            Address = address;
            Port = port;
            SessionName = sessionName;
            SessionToken = sessionToken;
        }
    }

    /// <summary>
    /// ネットワーク接続パラメータの設定と保持を行う静的クラス。
    /// SP ローカル・リモート、MP マッチメイキング、Dedicated Server の各接続経路を統一的に管理する。
    /// </summary>
    public static class SurvivorNetworkMatchConnector
    {
        /// <summary>SP ローカルセッションのデフォルト名。</summary>
        public const string DefaultLocalSessionName = "sp-local";

        /// <summary>SP リモートセッションのデフォルト名。</summary>
        public const string DefaultRemoteSessionName = "sp-remote";

        /// <summary>ローカルホストアドレス。</summary>
        public const string DefaultLocalAddress = "127.0.0.1";

        /// <summary>デフォルトポート番号。</summary>
        public const ushort DefaultPort = 7777;

        private static ConnectionParams _params;

        /// <summary>
        /// 期待プレイヤー数。SP=1、MP=ロビー設定値。
        /// タイトル画面のモード選択時またはロビー開始時にセットされる。
        /// </summary>
        public static int ExpectedPlayerCount { get; private set; } = 1;

        /// <summary>接続パラメータが設定済みかどうか。</summary>
        public static bool HasConnection => _params.Source != ConnectionSource.None;

        /// <summary>現在の接続元種別。</summary>
        public static ConnectionSource Source => _params.Source;

        /// <summary>
        /// 接続先サーバーアドレス。未設定時は <see cref="DefaultLocalAddress"/> を返す。
        /// </summary>
        public static string ServerAddress => _params.Address ?? DefaultLocalAddress;

        /// <summary>
        /// 接続先ポート番号。未設定時は <see cref="DefaultPort"/> を返す。
        /// </summary>
        public static ushort ServerPort => _params.Port != 0 ? _params.Port : DefaultPort;

        /// <summary>
        /// セッション名（Fusion セッション識別子）。未設定時は <see cref="DefaultLocalSessionName"/> を返す。
        /// </summary>
        public static string MatchId => _params.SessionName ?? DefaultLocalSessionName;

        /// <summary>
        /// セッショントークン。未設定時は空文字を返す。
        /// </summary>
        public static string SessionToken => _params.SessionToken ?? string.Empty;

        /// <summary>
        /// 後方互換プロパティ。クライアント接続経路（Local / Remote / Matchmaking）が設定済みかどうかを返す。
        /// SurvivorStageConnectScene の Phase 2 判定で使用する。
        /// </summary>
        public static bool HasMatchResult => _params.Source is ConnectionSource.Local
                                             or ConnectionSource.Remote
                                             or ConnectionSource.Matchmaking;

        /// <summary>
        /// 期待プレイヤー数を設定する。
        /// </summary>
        /// <param name="count">期待プレイヤー数。</param>
        public static void SetExpectedPlayerCount(int count) => ExpectedPlayerCount = count;

        /// <summary>
        /// SP ローカルサーバー接続として設定する。
        /// </summary>
        /// <param name="port">ローカルサーバーのポート番号。</param>
        /// <param name="sessionToken">セッショントークン（HMAC 認証用）。省略時は空文字。</param>
        /// <param name="sessionName">セッション名。省略時は <see cref="DefaultLocalSessionName"/>。</param>
        public static void ConfigureForLocalServer(ushort port, string sessionToken = "", string sessionName = DefaultLocalSessionName)
        {
            _params = new ConnectionParams(ConnectionSource.Local, DefaultLocalAddress, port, sessionName, sessionToken);
        }

        /// <summary>
        /// SP リモートサーバー接続として設定する。
        /// </summary>
        /// <param name="address">サーバーIPアドレスまたはホスト名。</param>
        /// <param name="port">サーバーポート番号。</param>
        /// <param name="sessionName">セッション名。省略時は <see cref="DefaultRemoteSessionName"/>。</param>
        /// <param name="sessionToken">セッショントークン（HMAC 認証用）。省略時は空文字。</param>
        public static void ConfigureForRemoteServer(string address, ushort port, string sessionName = DefaultRemoteSessionName, string sessionToken = "")
        {
            _params = new ConnectionParams(ConnectionSource.Remote, address, port, sessionName, sessionToken);
        }

        /// <summary>
        /// マッチメイキング結果から接続パラメータを設定する。
        /// </summary>
        /// <param name="result">マッチメイキングサーバーから受け取った <see cref="MatchResult"/>。</param>
        public static void ConfigureForMatchmaking(MatchResult result)
        {
            _params = new ConnectionParams(ConnectionSource.Matchmaking, result.ServerAddress, (ushort)result.ServerPort, result.MatchId, result.SessionToken);
        }

        /// <summary>
        /// Dedicated Server として起動するためのバインド設定を行う。
        /// </summary>
        /// <param name="port">バインドポート番号。</param>
        /// <param name="address">バインドアドレス。null 時は <see cref="DefaultLocalAddress"/>。</param>
        /// <param name="matchId">セッション名。null 時は <see cref="DefaultLocalSessionName"/>。</param>
        public static void ConfigureForDedicatedServer(ushort port, string address = null, string matchId = null)
        {
            _params = new ConnectionParams(ConnectionSource.DedicatedServer,
                address ?? DefaultLocalAddress, port,
                matchId ?? DefaultLocalSessionName,
                string.Empty);
        }

        /// <summary>
        /// 接続パラメータと期待プレイヤー数をリセットする。
        /// </summary>
        public static void Clear()
        {
            _params = default;
            ExpectedPlayerCount = 1;
        }
    }
}
