using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// セーブデータ暗号化用の鍵導出プロバイダー
    /// デバイスID + アプリ固有ソルトからPBKDF2で暗号化鍵とHMAC鍵を導出
    /// </summary>
    internal sealed class SaveDataKeyProvider
    {
        private const int KeySizeBytes = 32; // AES-256
        private const int Pbkdf2Iterations = 100_000;
        private static readonly byte[] AppSalt = Encoding.UTF8.GetBytes("Game.Shared.SaveData.v1");

        public byte[] EncryptionKey { get; }
        public byte[] HmacKey { get; }

        public SaveDataKeyProvider()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            EncryptionKey = DeriveKey(deviceId, "encryption");
            HmacKey = DeriveKey(deviceId, "hmac");
        }

        private static byte[] DeriveKey(string deviceId, string context)
        {
            // ソルト = AppSalt + コンテキスト文字列（暗号化鍵とHMAC鍵を独立導出）
            var contextBytes = Encoding.UTF8.GetBytes(context);
            var salt = new byte[AppSalt.Length + contextBytes.Length];
            Buffer.BlockCopy(AppSalt, 0, salt, 0, AppSalt.Length);
            Buffer.BlockCopy(contextBytes, 0, salt, AppSalt.Length, contextBytes.Length);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(deviceId),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(KeySizeBytes);
        }
    }
}
