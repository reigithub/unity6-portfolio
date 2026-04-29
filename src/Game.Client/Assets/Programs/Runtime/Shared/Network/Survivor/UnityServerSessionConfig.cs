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
        public ConnectionSource Source { get; private set; }

        /// <summary>接続先サーバーアドレス。</summary>
        public string ServerAddress { get; private set; }

        /// <summary>接続先ポート番号。</summary>
        public ushort ServerPort { get; private set; }

        /// <summary>セッション名（Fusion セッション識別子）。</summary>
        public string SessionName { get; private set; }

        /// <summary>セッショントークン（HMAC 認証用）。</summary>
        public string SessionToken { get; private set; }

        /// <summary>
        /// 期待プレイヤー数。SP=1、MP=ロビー設定値。
        /// タイトル画面のモード選択時またはロビー開始時にセットされる。
        /// </summary>
        public int PlayerCount { get; private set; } = 1;

        /// <summary>接続パラメータが設定済みかどうか。</summary>
        public bool HasConnection => Source != ConnectionSource.None;

        /// <summary>
        /// クライアント接続経路（Local / Remote / Matchmaking）が設定済みかどうかを返す。
        /// SurvivorStageConnectScene の Phase 2 判定で使用する。
        /// </summary>
        public bool IsClientConfigured => Source is ConnectionSource.Local or ConnectionSource.Remote or ConnectionSource.Matchmaking;

        /// <summary>
        /// 全パラメータを初期化する。未指定はデフォルト値で補完。
        /// </summary>
        /// <param name="source">接続元の種別。</param>
        /// <param name="address">接続先アドレス。null 時は <see cref="DefaultLocalAddress"/>。</param>
        /// <param name="port">接続先ポート番号。null 時は <see cref="DefaultPort"/>。</param>
        /// <param name="sessionName">セッション名。null 時は <see cref="DefaultLocalSessionName"/>。</param>
        /// <param name="sessionToken">セッショントークン。null 時は空文字。</param>
        /// <param name="playerCount">期待プレイヤー数。</param>
        public void Configure(ConnectionSource source, string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null)
        {
            Source = source;
            ServerAddress = address ?? DefaultLocalAddress;
            ServerPort = port ?? DefaultPort;
            SessionName = sessionName ?? (source is ConnectionSource.Remote ? DefaultRemoteSessionName : DefaultLocalSessionName);
            SessionToken = sessionToken ?? string.Empty;
            PlayerCount = playerCount ?? 1;
        }

        /// <summary>
        /// マッチメイキング結果から全パラメータを一括設定する。
        /// </summary>
        /// <param name="source">接続元の種別。</param>
        /// <param name="result">マッチメイキングサーバーから受け取った <see cref="MatchResult"/>。</param>
        /// <param name="playerCount">期待プレイヤー数。</param>
        public void Configure(ConnectionSource source, MatchResult result, int playerCount)
            => Configure(source, result.ServerAddress, (ushort)result.ServerPort, result.MatchId, result.SessionToken, playerCount);

        /// <summary>
        /// 指定パラメータのみ上書きする。null は既存値を維持。
        /// Dedicated Server のセッション開始時に sessionName のみ更新する用途で使用する。
        /// </summary>
        /// <param name="address">接続先アドレス。null で既存値を維持。</param>
        /// <param name="port">接続先ポート番号。null で既存値を維持。</param>
        /// <param name="sessionName">セッション名。null で既存値を維持。</param>
        /// <param name="sessionToken">セッショントークン。null で既存値を維持。</param>
        /// <param name="playerCount">期待プレイヤー数。</param>
        public void UpdateConfigure(string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null)
        {
            if (address != null) ServerAddress = address;
            if (port.HasValue) ServerPort = port.Value;
            if (sessionName != null) SessionName = sessionName;
            if (sessionToken != null) SessionToken = sessionToken;
            if (playerCount.HasValue) PlayerCount = playerCount.Value;
        }

        /// <summary>
        /// 接続パラメータと期待プレイヤー数をリセットする。
        /// </summary>
        public void Clear()
        {
            Source = ConnectionSource.None;
            ServerAddress = null;
            ServerPort = 0;
            SessionName = null;
            SessionToken = null;
            PlayerCount = 1;
        }

        public bool IsLocalAddress(string address)
            => string.IsNullOrEmpty(address) || address == "localhost" || address == DefaultLocalAddress;
    }
}
