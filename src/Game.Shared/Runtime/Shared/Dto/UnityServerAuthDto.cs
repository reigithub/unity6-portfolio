using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// Unity Dedicated Server 接続トークン発行レスポンス DTO。
    /// SP/MP 共通で Game.Server から返却される。
    /// </summary>
    [MessagePackObject]
    public class UnityServerAuthResponse
    {
        /// <summary>
        /// Fusion ConnectionToken に設定する HMAC 署名付きセッショントークン。
        /// Dedicated Server 側で検証される。
        /// </summary>
        [Key(0)]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Fusion セッション識別子（SessionName）。
        /// SP では一意の UUID ベースの名前が割り当てられる。
        /// MP ではマッチメイキング結果の MatchId と同一。
        /// </summary>
        [Key(1)]
        public string SessionName { get; set; } = string.Empty;

        /// <summary>
        /// 割り当てられた DS のアドレス（IP またはホスト名）。
        /// DS 割り当てが行われた場合（stageId &gt; 0）に設定される。
        /// 空文字列の場合は <see cref="GameEnvironmentConfig"/> のフォールバックを使用する。
        /// </summary>
        [Key(2)]
        public string ServerAddress { get; set; } = string.Empty;

        /// <summary>
        /// 割り当てられた DS の Fusion ゲームポート番号。
        /// DS 割り当てが行われた場合（stageId &gt; 0）に設定される。
        /// 0 の場合は <see cref="GameEnvironmentConfig"/> のフォールバックを使用する。
        /// </summary>
        [Key(3)]
        public int ServerPort { get; set; }
    }
}
