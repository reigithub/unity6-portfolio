using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// PBKDF2鍵導出とプロセス内静的キャッシュを提供する鍵プロバイダーの共通基底クラス
    /// 派生クラスは KeySourceId・Salt世代辞書・鍵材料文字列 を供給する
    /// </summary>
    public abstract class SaveDataKeyProviderBase : ISaveDataKeyProvider
    {
        private const int KeySizeBytes = 32; // AES-256
        private const int Pbkdf2Iterations = 100_000;

        // プロバイダー種別（KeySourceId）× Salt世代 × 用途ごとに1回だけPBKDF2導出するための静的キャッシュ
        // KeySourceIdをキーに含めるのはプロバイダー種別間の衝突を防ぐため
        private static readonly ConcurrentDictionary<(byte KeySourceId, byte SaltVersion, string Context), Lazy<byte[]>> _keyCache = new();

        /// <inheritdoc/>
        public abstract byte KeySourceId { get; }

        /// <inheritdoc/>
        public abstract byte CurrentSaltVersion { get; }

        /// <summary>
        /// Salt世代ごとの固定文字列辞書（派生クラスが供給）
        /// </summary>
        protected abstract IReadOnlyDictionary<byte, string> SaltVersions { get; }

        /// <summary>
        /// PBKDF2の鍵材料文字列（デバイスID、アプリ埋め込みシークレット等。派生クラスが供給）
        /// </summary>
        protected abstract string SecretMaterial { get; }

        /// <inheritdoc/>
        public byte[] GetEncryptionKey(byte saltVersion) => GetCachedKey(saltVersion, "encryption");

        /// <inheritdoc/>
        public byte[] GetHmacKey(byte saltVersion) => GetCachedKey(saltVersion, "hmac");

        /// <summary>
        /// CurrentSaltVersionのencryption/hmac鍵をスレッドプール上で事前導出しておく
        /// PBKDF2初回実行によるメインスレッドブロックを避けるための事前ウォームアップ
        /// WebGLはスレッドを使用できないため何もしない
        /// </summary>
        public void Prewarm()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return;

            var saltVersion = CurrentSaltVersion;
            UniTask.RunOnThreadPool(() =>
            {
                GetEncryptionKey(saltVersion);
                GetHmacKey(saltVersion);
            }).Forget();
        }

        private byte[] GetCachedKey(byte saltVersion, string context)
        {
            if (!SaltVersions.TryGetValue(saltVersion, out var saltSeed))
            {
                return null;
            }

            var cacheKey = (KeySourceId, saltVersion, context);
            var lazy = _keyCache.GetOrAdd(cacheKey, _ => new Lazy<byte[]>(() => DeriveKey(SecretMaterial, saltSeed, context)));
            return lazy.Value;
        }

        private static byte[] DeriveKey(string secretMaterial, string saltSeed, string context)
        {
            // ソルト = saltSeed + コンテキスト文字列（暗号化鍵とHMAC鍵を独立導出）
            var saltSeedBytes = Encoding.UTF8.GetBytes(saltSeed);
            var contextBytes = Encoding.UTF8.GetBytes(context);
            var salt = new byte[saltSeedBytes.Length + contextBytes.Length];
            Buffer.BlockCopy(saltSeedBytes, 0, salt, 0, saltSeedBytes.Length);
            Buffer.BlockCopy(contextBytes, 0, salt, saltSeedBytes.Length, contextBytes.Length);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(secretMaterial),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(KeySizeBytes);
        }
    }
}
