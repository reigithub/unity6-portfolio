using System;
using System.Text;

namespace Game.Library.Shared.RequestSigning
{
    /// <summary>
    /// HMAC セッショントークンのパース結果
    /// </summary>
    public class SessionTokenParseResult
    {
        public string UserId { get; init; }
        public string MatchId { get; init; }
        public DateTimeOffset IssuedAt { get; init; }
    }

    /// <summary>
    /// HMAC 署名付きセッショントークンの生成・検証ユーティリティ。
    /// Game.Realtime (トークン発行) と Dedicated Server (トークン検証) の両方から使用。
    /// トークン形式: {Base64Url(userId|matchId|unixTimestamp)}.{HMAC-SHA256-base64url}
    /// ※ Fusion ConnectionToken の 128 バイト制限に収まるよう署名を base64url 形式で出力する（~105B）。
    /// </summary>
    public static class SessionTokenHelper
    {
        public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        /// <summary>
        /// HMAC 署名付きトークンを生成する。
        /// 署名形式: base64url（Fusion ConnectionToken 128B 制限対応）
        /// </summary>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="matchId">マッチID</param>
        /// <returns>署名付きトークン文字列</returns>
        public static string CreateToken(byte[] secretKey, string userId, string matchId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $"{userId}|{matchId}|{timestamp}";
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            var signatureBytes = HmacRequestSigner.ComputeSignatureBytes(secretKey, payload);
            var signatureB64 = Base64UrlEncode(signatureBytes);
            return $"{payloadB64}.{signatureB64}";
        }

        /// <summary>
        /// トークンの HMAC 署名を検証し、ペイロードを返す。
        /// Valkey 不要、シークレットキーのみで完結。
        /// base64url 形式の署名を検証する。
        /// </summary>
        /// <param name="token">検証するトークン</param>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <returns>検証成功時はパース結果、失敗時は null</returns>
        public static SessionTokenParseResult ParseAndVerify(string token, byte[] secretKey)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var dotIndex = token.LastIndexOf('.');
            if (dotIndex < 0) return null;

            var payloadB64 = token.Substring(0, dotIndex);
            var signaturePart = token.Substring(dotIndex + 1);

            byte[] payloadBytes;
            try
            {
                payloadBytes = Base64UrlDecode(payloadB64);
            }
            catch
            {
                return null;
            }

            var payload = Encoding.UTF8.GetString(payloadBytes);

            // base64url 形式で検証
            if (!VerifyBase64UrlSignature(secretKey, payload, signaturePart))
                return null;

            var parts = payload.Split('|');
            if (parts.Length != 3 || !long.TryParse(parts[2], out var unixTime))
                return null;

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            if (DateTimeOffset.UtcNow - issuedAt > DefaultExpiry)
                return null;

            return new SessionTokenParseResult
            {
                UserId = parts[0],
                MatchId = parts[1],
                IssuedAt = issuedAt,
            };
        }

        private static bool VerifyBase64UrlSignature(byte[] secretKey, string payload, string providedSignature)
        {
            var expectedBytes = HmacRequestSigner.ComputeSignatureBytes(secretKey, payload);
            var expectedB64 = Base64UrlEncode(expectedBytes);
            return HmacRequestSigner.CryptographicEquals(expectedB64, providedSignature);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace("-", "+").Replace("_", "/");
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }

            return Convert.FromBase64String(s);
        }
    }
}
