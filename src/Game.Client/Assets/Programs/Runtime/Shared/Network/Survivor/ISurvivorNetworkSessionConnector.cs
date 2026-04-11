using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// ネットワーク接続パラメータの設定と保持を行うインターフェース。
    /// SP ローカル・リモート、MP マッチメイキング、Dedicated Server の各接続経路を統一的に管理する。
    /// Configure（全パラメータ初期化）と UpdateConfigure（部分更新）に分離し、
    /// ライフサイクルの異なるサーバーレベルパラメータとセッションレベルパラメータを明示的に管理する。
    /// </summary>
    public interface ISurvivorNetworkSessionConnector
    {
        /// <summary>接続先サーバーアドレス。</summary>
        string ServerAddress { get; }

        /// <summary>接続先ポート番号。</summary>
        ushort ServerPort { get; }

        /// <summary>セッション名（Fusion セッション識別子）。</summary>
        string SessionName { get; }

        /// <summary>セッショントークン（HMAC 認証用）。</summary>
        string SessionToken { get; }

        /// <summary>期待プレイヤー数。SP=1、MP=ロビー設定値。</summary>
        int MaxPlayerCount { get; }

        /// <summary>
        /// クライアント接続経路（Local / Remote / Matchmaking）が設定済みかどうかを返す。
        /// SurvivorStageConnectScene の Phase 2 判定で使用する。
        /// </summary>
        bool IsClientConfigured { get; }

        /// <summary>全パラメータを初期化する。未指定はデフォルト値で補完。</summary>
        void Configure(ConnectionSource source, string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null);

        /// <summary>マッチメイキング結果から全パラメータを一括設定する。</summary>
        void Configure(ConnectionSource source, MatchResult result, int playerCount);

        /// <summary>
        /// 指定パラメータのみ上書きする。null は既存値を維持。
        /// Dedicated Server のセッション開始時に matchId のみ更新する用途で使用する。
        /// </summary>
        void UpdateConfigure(string address = null, ushort? port = null, string sessionName = null, string sessionToken = null, int? playerCount = null);

        /// <summary>接続パラメータと期待プレイヤー数をリセットする。</summary>
        void Clear();

        /// <summary>指定アドレスがローカル（空文字 / localhost / 127.0.0.1）かどうかを判定する。</summary>
        bool IsLocalAddress(string address);
    }
}
