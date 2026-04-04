using System;
using System.Security.Cryptography;
using System.Text;

namespace Game.Library.Shared.RequestSigning
{
    public static class HmacRequestSigner
    {
        /// <summary>
        /// Canonical String を構築する
        /// 形式: {METHOD}\n{PATH}\n{TIMESTAMP}\n{NONCE}\n{BODY_SHA256_HEX}
        /// </summary>
        public static string BuildCanonicalString(string method, string path, long timestamp, string nonce, byte[] bodyBytes)
        {
            var bodyHash = ComputeBodyHash(bodyBytes);
            return $"{method.ToUpperInvariant()}\n{path}\n{timestamp}\n{nonce}\n{bodyHash}";
        }

        /// <summary>
        /// ボディの SHA256 ハッシュを小文字 hex で返す
        /// </summary>
        public static string ComputeBodyHash(byte[] body)
        {
            if (body == null)
            {
                body = Array.Empty<byte>();
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(body);
            return BytesToHex(hash);
        }

        /// <summary>
        /// HMAC-SHA256 署名を生成する（小文字 hex）
        /// </summary>
        public static string ComputeSignature(byte[] secretKey, string canonicalString)
        {
            var hash = ComputeSignatureBytes(secretKey, canonicalString);
            return BytesToHex(hash);
        }

        /// <summary>
        /// HMAC-SHA256 署名の生バイト列を返す。
        /// </summary>
        internal static byte[] ComputeSignatureBytes(byte[] secretKey, string data)
        {
            using var hmac = new HMACSHA256(secretKey);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// 署名を検証する（定時間比較）
        /// </summary>
        public static bool VerifySignature(byte[] secretKey, string canonicalString, string providedSignature)
        {
            var expectedSignature = ComputeSignature(secretKey, canonicalString);
            return CryptographicEquals(expectedSignature, providedSignature);
        }

        /// <summary>
        /// 定時間文字列比較（タイミング攻撃対策）
        /// </summary>
        internal static bool CryptographicEquals(string a, string b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
