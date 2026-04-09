using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// Dedicated Server 自己登録リクエスト DTO。
    /// DS 起動時に Game.Server の <c>POST /api/unity-server/register</c> へ送信する。
    /// </summary>
    [MessagePackObject]
    public class UnityServerRegistrationRequest
    {
        /// <summary>
        /// Dedicated Server の一意識別子（起動時に割り当て）。
        /// </summary>
        [Key(0)]
        public string DsId { get; set; } = string.Empty;

        /// <summary>
        /// DS のアドレス（IP またはホスト名）。クライアント接続先として使用。
        /// </summary>
        [Key(1)]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Fusion ゲームポート番号。
        /// </summary>
        [Key(2)]
        public int GamePort { get; set; }

        /// <summary>
        /// ヘルスチェックポート番号。
        /// </summary>
        [Key(3)]
        public int HealthPort { get; set; }
    }
}
