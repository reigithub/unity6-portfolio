using MessagePack;

namespace Game.Library.Shared.Dto
{
    /// <summary>
    /// Dedicated Server のヘルス・ステータス情報 DTO。
    /// Game.Server が DS 一覧を管理する際に Valkey に保存される。
    /// </summary>
    [MessagePackObject]
    public class UnityServerHealthResponse
    {
        /// <summary>
        /// Dedicated Server の一意識別子。
        /// </summary>
        [Key(0)]
        public string DsId { get; set; } = string.Empty;

        /// <summary>
        /// DS の現在ステータス。"idle"（待機中）または "active"（セッション実行中）。
        /// </summary>
        [Key(1)]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 現在実行中のマッチID。<see cref="Status"/> が "idle" の場合は空文字列。
        /// </summary>
        [Key(2)]
        public string CurrentMatchId { get; set; } = string.Empty;

        /// <summary>
        /// DS の起動からの経過秒数。
        /// </summary>
        [Key(3)]
        public long UptimeSeconds { get; set; }
    }
}
