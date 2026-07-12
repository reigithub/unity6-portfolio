namespace Game.Shared.SaveData
{
    /// <summary>
    /// セーブデータ暗号化鍵の導出戦略を表すインターフェース
    /// KeySourceIdで鍵の由来（デバイス固定/アプリ共有等）を識別し、
    /// SaltVersionで鍵導出パラメータ（ソルト）の世代を管理する
    /// </summary>
    public interface ISaveDataKeyProvider
    {
        /// <summary>
        /// 鍵の由来を識別するID。暗号化ファイルのヘッダに記録され、復号時のプロバイダー解決に使用される
        /// </summary>
        byte KeySourceId { get; }

        /// <summary>
        /// このプロバイダーが書き込み時に使用する最新のSalt世代
        /// </summary>
        byte CurrentSaltVersion { get; }

        /// <summary>
        /// 指定したSalt世代の暗号化鍵を取得する
        /// </summary>
        /// <param name="saltVersion">Salt世代</param>
        /// <returns>32バイトのAES鍵。未知の世代の場合はnull</returns>
        byte[] GetEncryptionKey(byte saltVersion);

        /// <summary>
        /// 指定したSalt世代のHMAC鍵を取得する
        /// </summary>
        /// <param name="saltVersion">Salt世代</param>
        /// <returns>32バイトのHMAC鍵。未知の世代の場合はnull</returns>
        byte[] GetHmacKey(byte saltVersion);
    }
}
