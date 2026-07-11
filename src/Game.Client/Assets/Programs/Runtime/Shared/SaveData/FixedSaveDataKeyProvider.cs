using System.Collections.Generic;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// テスト用の固定鍵プロバイダー。あらかじめ用意したbyte配列鍵をそのまま返す
    /// </summary>
    internal sealed class FixedSaveDataKeyProvider : ISaveDataKeyProvider
    {
        private const byte SupportedSaltVersion = 1;

        private readonly IReadOnlyDictionary<byte, (byte[] EncryptionKey, byte[] HmacKey)> _keysBySaltVersion;

        /// <summary>
        /// コンストラクタ（単一Salt世代=1）
        /// </summary>
        /// <param name="encryptionKey">固定の暗号化鍵</param>
        /// <param name="hmacKey">固定のHMAC鍵</param>
        /// <param name="keySourceId">KeySourceId（複数のFixedプロバイダーを区別するテスト用途で指定可能。既定は0x00）</param>
        internal FixedSaveDataKeyProvider(byte[] encryptionKey, byte[] hmacKey, byte keySourceId = 0x00)
            : this(new Dictionary<byte, (byte[] EncryptionKey, byte[] HmacKey)> { { SupportedSaltVersion, (encryptionKey, hmacKey) } },
                SupportedSaltVersion,
                keySourceId)
        {
        }

        /// <summary>
        /// コンストラクタ（複数Salt世代対応。世代アップグレードのテスト用途）
        /// </summary>
        /// <param name="keysBySaltVersion">Salt世代ごとの暗号化鍵/HMAC鍵マップ</param>
        /// <param name="currentSaltVersion">書き込みに使用する最新Salt世代</param>
        /// <param name="keySourceId">KeySourceId（既定は0x00）</param>
        internal FixedSaveDataKeyProvider(
            IReadOnlyDictionary<byte, (byte[] EncryptionKey, byte[] HmacKey)> keysBySaltVersion,
            byte currentSaltVersion,
            byte keySourceId = 0x00)
        {
            _keysBySaltVersion = keysBySaltVersion;
            CurrentSaltVersion = currentSaltVersion;
            KeySourceId = keySourceId;
        }

        /// <inheritdoc/>
        public byte KeySourceId { get; }

        /// <inheritdoc/>
        public byte CurrentSaltVersion { get; }

        /// <inheritdoc/>
        public byte[] GetEncryptionKey(byte saltVersion)
            => _keysBySaltVersion.TryGetValue(saltVersion, out var keys) ? keys.EncryptionKey : null;

        /// <inheritdoc/>
        public byte[] GetHmacKey(byte saltVersion)
            => _keysBySaltVersion.TryGetValue(saltVersion, out var keys) ? keys.HmacKey : null;
    }
}
