using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// AES-256-CBC暗号化 + HMAC-SHA256改竄検知を提供するセーブデータストレージ デコレーター
    /// 既存のISaveDataStorageをラップし、パス解決・Exists・Deleteは内部ストレージに委譲
    /// </summary>
    public class EncryptedSaveDataStorage : ISaveDataStorage
    {
        // ファイルフォーマット: [Magic 4B] [Version 1B] [IV 16B] [HMAC 32B] [暗号化データ NB]
        private static readonly byte[] Magic = { 0x45, 0x53, 0x44, 0x53 }; // "ESDS"
        private const byte FormatVersion = 1;
        private const int IvSizeBytes = 16;
        private const int HmacSizeBytes = 32;
        private const int HeaderSize = 4 + 1 + IvSizeBytes + HmacSizeBytes; // 53 bytes

        private const int MaxRetryCount = 3;
        private const int RetryDelayMs = 100;

        private readonly ISaveDataStorage _inner;
        private readonly byte[] _encryptionKey;
        private readonly byte[] _hmacKey;

        private readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly object _lockDictionaryLock = new();

        /// <summary>
        /// 本番用コンストラクタ（SaveDataKeyProviderで鍵を自動生成）
        /// </summary>
        public EncryptedSaveDataStorage(ISaveDataStorage inner)
        {
            _inner = inner;
            var keyProvider = new SaveDataKeyProvider();
            _encryptionKey = keyProvider.EncryptionKey;
            _hmacKey = keyProvider.HmacKey;
        }

        /// <summary>
        /// テスト用コンストラクタ（鍵を直接指定）
        /// </summary>
        public EncryptedSaveDataStorage(ISaveDataStorage inner, byte[] encryptionKey, byte[] hmacKey)
        {
            _inner = inner;
            _encryptionKey = encryptionKey;
            _hmacKey = hmacKey;
        }

        public string BasePath => _inner.BasePath;

        private SemaphoreSlim GetFileLock(string key)
        {
            lock (_lockDictionaryLock)
            {
                if (!_fileLocks.TryGetValue(key, out var semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    _fileLocks[key] = semaphore;
                }
                return semaphore;
            }
        }

        public async UniTask<T> LoadAsync<T>(string key) where T : class
        {
            return await LoadAsync<T>(key, default);
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue) where T : class
        {
            var path = _inner.GetFullPath(key);

            try
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[EncryptedSaveDataStorage] File not found: {key}");
                    return defaultValue;
                }

                var fileBytes = await File.ReadAllBytesAsync(path);

                if (IsEncryptedFormat(fileBytes))
                {
                    return LoadEncrypted<T>(fileBytes, key, defaultValue);
                }

                // レガシー（非暗号化）形式: 平文デシリアライズ後、暗号化形式で再保存
                Debug.Log($"[EncryptedSaveDataStorage] Legacy format detected for {key}, migrating to encrypted format.");
                var legacyData = MemoryPackSerializer.Deserialize<T>(fileBytes);
                if (legacyData != null)
                {
                    await SaveAsync(key, legacyData);
                }
                return legacyData ?? defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Failed to load {key}: {e.Message}");
                return defaultValue;
            }
        }

        private T LoadEncrypted<T>(byte[] fileBytes, string key, T defaultValue) where T : class
        {
            if (fileBytes.Length < HeaderSize)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] File too small for {key}");
                return defaultValue;
            }

            var version = fileBytes[4];
            if (version != FormatVersion)
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Unknown format version {version} for {key}");
                return defaultValue;
            }

            // IV, HMAC, 暗号化データを抽出
            var iv = new byte[IvSizeBytes];
            Buffer.BlockCopy(fileBytes, 5, iv, 0, IvSizeBytes);

            var storedHmac = new byte[HmacSizeBytes];
            Buffer.BlockCopy(fileBytes, 5 + IvSizeBytes, storedHmac, 0, HmacSizeBytes);

            var encryptedData = new byte[fileBytes.Length - HeaderSize];
            Buffer.BlockCopy(fileBytes, HeaderSize, encryptedData, 0, encryptedData.Length);

            // HMAC検証（IV + 暗号化データ）
            var computedHmac = ComputeHmac(iv, encryptedData);
            if (!CryptographicEquals(storedHmac, computedHmac))
            {
                Debug.LogWarning($"[EncryptedSaveDataStorage] HMAC verification failed for {key}. Data may be tampered.");
                return defaultValue;
            }

            // AES復号
            var plainBytes = DecryptAes(encryptedData, iv);
            var data = MemoryPackSerializer.Deserialize<T>(plainBytes);
            Debug.Log($"[EncryptedSaveDataStorage] Loaded (encrypted): {key}");
            return data ?? defaultValue;
        }

        public async UniTask SaveAsync<T>(string key, T data) where T : class
        {
            var path = _inner.GetFullPath(key);
            var fileLock = GetFileLock(key);
            await fileLock.WaitAsync();
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var plainBytes = MemoryPackSerializer.Serialize(data);

                // AES暗号化
                var iv = GenerateIv();
                var encryptedData = EncryptAes(plainBytes, iv);

                // HMAC署名（IV + 暗号化データ）
                var hmac = ComputeHmac(iv, encryptedData);

                // ファイルフォーマット組み立て
                var fileBytes = new byte[HeaderSize + encryptedData.Length];
                Buffer.BlockCopy(Magic, 0, fileBytes, 0, Magic.Length);
                fileBytes[4] = FormatVersion;
                Buffer.BlockCopy(iv, 0, fileBytes, 5, IvSizeBytes);
                Buffer.BlockCopy(hmac, 0, fileBytes, 5 + IvSizeBytes, HmacSizeBytes);
                Buffer.BlockCopy(encryptedData, 0, fileBytes, HeaderSize, encryptedData.Length);

                // リトライ付き書き込み
                Exception lastException = null;
                for (int retry = 0; retry < MaxRetryCount; retry++)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(path, fileBytes);
                        Debug.Log($"[EncryptedSaveDataStorage] Saved (encrypted): {key} ({fileBytes.Length} bytes)");
                        return;
                    }
                    catch (IOException ex) when (ex.Message.Contains("Sharing violation"))
                    {
                        lastException = ex;
                        Debug.LogWarning(
                            $"[EncryptedSaveDataStorage] Retry {retry + 1}/{MaxRetryCount} for {key}: {ex.Message}");
                        await UniTask.Delay(RetryDelayMs * (retry + 1));
                    }
                }

                if (lastException != null)
                {
                    Debug.LogError(
                        $"[EncryptedSaveDataStorage] Failed to save {key} after {MaxRetryCount} retries: {lastException.Message}");
                    throw lastException;
                }
            }
            catch (Exception e) when (!e.Message.Contains("Sharing violation"))
            {
                Debug.LogError($"[EncryptedSaveDataStorage] Failed to save {key}: {e.Message}");
                throw;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public UniTask DeleteAsync(string key) => _inner.DeleteAsync(key);

        public bool Exists(string key) => _inner.Exists(key);

        public string GetFullPath(string key) => _inner.GetFullPath(key);

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

        private byte[] ComputeHmac(byte[] iv, byte[] encryptedData)
        {
            using var hmac = new HMACSHA256(_hmacKey);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);
            hmac.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            return hmac.Hash;
        }

        private byte[] EncryptAes(byte[] plainBytes, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _encryptionKey;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        private byte[] DecryptAes(byte[] cipherBytes, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _encryptionKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }

        private static byte[] GenerateIv()
        {
            var iv = new byte[IvSizeBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }

        /// <summary>
        /// タイミング攻撃を防ぐための定数時間比較
        /// </summary>
        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        #endregion
    }
}
