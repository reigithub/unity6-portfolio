using System;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// AES-256-CBC暗号化 + HMAC-SHA256改竄検知を提供するセーブデータストレージ デコレーター
    /// 既存のISaveDataStorageをラップし、パス解決・Exists・Deleteは内部ストレージに委譲する
    /// 鍵はISaveDataKeyProviderに委譲することで、デバイス固定/アプリ共有などの戦略を差し替え可能にする
    /// ISessionSaveDataStorageの実装はSessionSaveDataStorage（device-bound構成を焼き込んだ派生型）が担う
    /// </summary>
    public class EncryptedSaveDataStorage : ISaveDataStorage
    {
        // ファイルフォーマット: [Magic 4B][FormatVersion=1 1B][KeySourceId 1B][SaltVersion 1B][IV 16B][HMAC 32B][暗号化データ NB]
        private static readonly byte[] Magic = { 0x45, 0x53, 0x44, 0x53 }; // "ESDS"

        private const byte FormatVersion = 1;

        private const int IvSizeBytes = 16;
        private const int HmacSizeBytes = 32;

        private const int HeaderSize = 4 + 1 + 1 + 1 + IvSizeBytes + HmacSizeBytes; // 55
        private const int HmacSignedRegionSize = 4 + 1 + 1 + 1 + IvSizeBytes; // 23 (Magic+FormatVersion+KeySourceId+SaltVersion+IV)

        private readonly ISaveDataStorage _inner;
        private readonly ISaveDataKeyProvider _provider;

        /// <summary>
        /// 鍵プロバイダーを指定するコンストラクタ
        /// </summary>
        /// <param name="inner">物理I/Oを担う内部ストレージ</param>
        /// <param name="provider">読み書き共通で使用する鍵プロバイダー</param>
        public EncryptedSaveDataStorage(ISaveDataStorage inner, ISaveDataKeyProvider provider)
        {
            _inner = inner;
            _provider = provider;
        }

        public string BasePath => _inner.BasePath;

        public UniTask<T> LoadAsync<T>(string key) where T : class
            => LoadAsync<T>(key, default);

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue) where T : class
        {
            var bytes = await LoadBytesAsync(key);
            if (bytes == null)
            {
                return defaultValue;
            }

            try
            {
                var data = MemoryPackSerializer.Deserialize<T>(bytes);
                return data ?? defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Failed to deserialize {key}: {e.Message}");
                return defaultValue;
            }
        }

        public async UniTask SaveAsync<T>(string key, T data) where T : class
        {
            var bytes = MemoryPackSerializer.Serialize(data);
            await SaveBytesAsync(key, bytes);
        }

        /// <summary>
        /// 生バイト列（平文）としてセーブデータを読み込む
        /// ESDS形式なら検証・復号した平文を、レガシー平文形式ならそのまま返す
        /// 読み込み成功後、必要であれば暗号化形式への移行・Salt世代アップグレード再保存を行う
        /// （再保存の失敗が読み込み結果を破棄することは無い）
        /// </summary>
        public async UniTask<byte[]> LoadBytesAsync(string key)
        {
            var fileBytes = await _inner.LoadBytesAsync(key);
            if (fileBytes == null)
            {
                return null;
            }

            if (!IsEncryptedFormat(fileBytes))
            {
                // レガシー（非暗号化）形式: 平文のまま返却しつつ、providerで暗号化形式へ再保存する
                Debug.Log($"[EncryptedSaveDataStorage] Legacy format detected for {key}, migrating to encrypted format.");
                await TrySaveBytesAsync(key, fileBytes);
                return fileBytes;
            }

            if (fileBytes.Length < 5)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] File too small for {key}");
                return null;
            }

            var formatVersion = fileBytes[4];
            if (formatVersion != FormatVersion)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Unknown format version {formatVersion} for {key}");
                return null;
            }

            var plainBytes = LoadEncrypted(fileBytes, key, out var saltVersion);
            if (plainBytes == null)
            {
                return null;
            }

            Debug.Log($"[EncryptedSaveDataStorage] Loaded (encrypted): {key}");

            if (NeedsUpgrade(saltVersion))
            {
                await TrySaveBytesAsync(key, plainBytes);
            }

            return plainBytes;
        }

        /// <summary>
        /// 生バイト列（平文）を暗号化（providerの最新Salt世代）して保存する
        /// </summary>
        public async UniTask SaveBytesAsync(string key, byte[] data)
        {
            var saltVersion = _provider.CurrentSaltVersion;
            var encryptionKey = _provider.GetEncryptionKey(saltVersion);
            var hmacKey = _provider.GetHmacKey(saltVersion);

            var iv = GenerateIv();
            var cipherBytes = EncryptAes(data, iv, encryptionKey);

            var fileBytes = new byte[HeaderSize + cipherBytes.Length];
            Buffer.BlockCopy(Magic, 0, fileBytes, 0, Magic.Length);
            fileBytes[4] = FormatVersion;
            fileBytes[5] = _provider.KeySourceId;
            fileBytes[6] = saltVersion;
            Buffer.BlockCopy(iv, 0, fileBytes, 7, IvSizeBytes);
            Buffer.BlockCopy(cipherBytes, 0, fileBytes, HeaderSize, cipherBytes.Length);

            // HMAC署名対象（ヘッダ先頭23B + 暗号文）はfileBytesへ直接参照する（中間コピー無し）
            var hmac = ComputeHmac(hmacKey, fileBytes, HmacSignedRegionSize, fileBytes, HeaderSize, cipherBytes.Length);
            Buffer.BlockCopy(hmac, 0, fileBytes, 7 + IvSizeBytes, HmacSizeBytes);

            await _inner.SaveBytesAsync(key, fileBytes);
            Debug.Log($"[EncryptedSaveDataStorage] Saved (encrypted): {key} ({fileBytes.Length} bytes)");
        }

        public UniTask DeleteAsync(string key) => _inner.DeleteAsync(key);

        public bool Exists(string key) => _inner.Exists(key);

        public string GetFullPath(string key) => _inner.GetFullPath(key);

        /// <summary>
        /// ロード成功後の自動再保存を行うか判定する（providerのSalt世代アップグレードのみ。AppSaltローテーション時の移行経路）
        /// </summary>
        private bool NeedsUpgrade(byte saltVersion) => saltVersion != _provider.CurrentSaltVersion;

        /// <summary>
        /// 再保存を試みる。失敗してもロード結果（呼び出し元が保持する平文バイト列）を破棄しないよう、個別にtry/catchする
        /// </summary>
        private async UniTask TrySaveBytesAsync(string key, byte[] plainBytes)
        {
            try
            {
                await SaveBytesAsync(key, plainBytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Failed to re-save {key} after load: {e.Message}");
            }
        }

        /// <summary>
        /// 暗号化形式を検証・復号する。ヘッダのKeySourceIdがproviderと一致するか検証し、SaltVersionで鍵を取得する
        /// HMAC検証に成功するまで暗号化鍵の導出は行わない（検証失敗パスでの無駄なPBKDF2実行を避けるため）
        /// </summary>
        private byte[] LoadEncrypted(byte[] fileBytes, string key, out byte saltVersion)
        {
            saltVersion = 0;

            if (fileBytes.Length < HeaderSize)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] File too small for {key}");
                return null;
            }

            var keySourceId = fileBytes[5];
            saltVersion = fileBytes[6];

            if (keySourceId != _provider.KeySourceId)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Unknown key source 0x{keySourceId:X2} for {key}");
                return null;
            }

            var hmacKey = _provider.GetHmacKey(saltVersion);
            if (hmacKey == null)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Unknown salt version {saltVersion} for {key}");
                return null;
            }

            var cipherLength = fileBytes.Length - HeaderSize;
            var computedHmac = ComputeHmac(hmacKey, fileBytes, HmacSignedRegionSize, fileBytes, HeaderSize, cipherLength);

            if (!CryptographicEquals(computedHmac, fileBytes, 7 + IvSizeBytes))
            {
                Debug.LogError($"[EncryptedSaveDataStorage] HMAC verification failed for {key}. Data may be tampered.");
                return null;
            }

            var encryptionKey = _provider.GetEncryptionKey(saltVersion);
            if (encryptionKey == null)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Unknown salt version {saltVersion} for {key}");
                return null;
            }

            var iv = new byte[IvSizeBytes];
            Buffer.BlockCopy(fileBytes, 7, iv, 0, IvSizeBytes);

            return DecryptAes(fileBytes, HeaderSize, cipherLength, iv, encryptionKey);
        }

        #region Crypto Helpers

        private static bool IsEncryptedFormat(byte[] data)
        {
            if (data.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// HMAC-SHA256を計算する（prefix[0,prefixCount) + payload[payloadOffset,payloadOffset+payloadCount) の順に署名）
        /// 中間コピーを避けるため、呼び出し元のバッファへ直接オフセット参照する
        /// </summary>
        private static byte[] ComputeHmac(byte[] hmacKey, byte[] prefix, int prefixCount, byte[] payload, int payloadOffset, int payloadCount)
        {
            using var hmac = new HMACSHA256(hmacKey);
            hmac.TransformBlock(prefix, 0, prefixCount, null, 0);
            hmac.TransformFinalBlock(payload, payloadOffset, payloadCount);
            return hmac.Hash;
        }

        private static byte[] EncryptAes(byte[] plainBytes, byte[] iv, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        /// <summary>
        /// AES復号する。cipherBuffer[cipherOffset,cipherOffset+cipherCount)を直接参照し、中間コピーを避ける
        /// </summary>
        private static byte[] DecryptAes(byte[] cipherBuffer, int cipherOffset, int cipherCount, byte[] iv, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipherBuffer, cipherOffset, cipherCount);
        }

        private static byte[] GenerateIv()
        {
            var iv = new byte[IvSizeBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }

        /// <summary>
        /// タイミング攻撃を防ぐための定数時間比較。b[bOffset,bOffset+a.Length)と比較し、storedHmac側の中間コピーを避ける
        /// </summary>
        private static bool CryptographicEquals(byte[] a, byte[] b, int bOffset)
        {
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[bOffset + i];
            }
            return diff == 0;
        }

        #endregion
    }
}
