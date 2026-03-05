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
    /// トークン形式: {Base64Url(userId|matchId|unixTimestamp)}.{HMAC-SHA256-hex}
    /// </summary>
    public static class SessionTokenHelper
    {
        public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        /// <summary>
        /// HMAC 署名付きトークンを生成する。
        /// </summary>
        public static string CreateToken(byte[] secretKey, string userId, string matchId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $"{userId}|{matchId}|{timestamp}";
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            var signature = HmacRequestSigner.ComputeSignature(secretKey, payload);
            return $"{payloadB64}.{signature}";
        }

        /// <summary>
        /// トークンの HMAC 署名を検証し、ペイロードを返す。
        /// Valkey 不要、シークレットキーのみで完結。
        /// </summary>
        public static SessionTokenParseResult ParseAndVerify(string token, byte[] secretKey)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var dotIndex = token.LastIndexOf('.');
            if (dotIndex < 0) return null;

            var payloadB64 = token.Substring(0, dotIndex);
            var signature = token.Substring(dotIndex + 1);

            var payload = Encoding.UTF8.GetString(Base64UrlDecode(payloadB64));
            if (!HmacRequestSigner.VerifySignature(secretKey, payload, signature))
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
