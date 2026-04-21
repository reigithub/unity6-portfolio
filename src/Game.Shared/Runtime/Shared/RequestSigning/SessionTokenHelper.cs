using System;
using System.Buffers;
using MessagePack;

namespace Game.Library.Shared.RequestSigning
{
    /// <summary>
    /// HMAC セッショントークンのパース結果
    /// </summary>
    public class SessionTokenParseResult
    {
        public string UserId { get; init; } = string.Empty;
        public string SessionName { get; init; } = string.Empty;
        public DateTimeOffset IssuedAt { get; init; }
    }

    /// <summary>
    /// HMAC 署名付きセッショントークンの生成・検証ユーティリティ。
    /// Game.Server (トークン発行) と Dedicated Server (トークン検証) の両方から使用。
    /// トークン形式: MessagePack バイナリ（array(3): userId, sessionName, unixTimestamp） + HMAC-SHA256 32B
    /// Base64 文字列として HTTP レスポンスに格納し、Fusion ConnectionToken では直接バイナリを使用する。
    /// トークンサイズ: ~117B（Fusion ConnectionToken 128B 上限に収まる）
    /// </summary>
    public static class SessionTokenHelper
    {
        public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        private const int SignatureSize = 32;

        /// <summary>
        /// HMAC 署名付きトークンをバイナリで生成する。
        /// Fusion ConnectionToken として直接渡せる 128B 以内のバイト列を返す。
        /// </summary>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="sessionName">Fusion セッション名（SessionName）</param>
        /// <returns>署名付きトークンのバイト列</returns>
        public static byte[] CreateTokenBytes(byte[] secretKey, string userId, string sessionName)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payloadBytes = PackPayload(userId, sessionName, timestamp);
            var signature = HmacRequestSigner.ComputeSignatureBytes(secretKey, payloadBytes);

            var token = new byte[payloadBytes.Length + SignatureSize];
            Buffer.BlockCopy(payloadBytes, 0, token, 0, payloadBytes.Length);
            Buffer.BlockCopy(signature, 0, token, payloadBytes.Length, SignatureSize);

            System.Diagnostics.Debug.Assert(
                token.Length <= 128,
                $"[SessionTokenHelper] トークンサイズ {token.Length}B が Fusion ConnectionToken 上限 128B を超えています。BinaryPrimitives 方式への移行を検討してください。");

            return token;
        }

        /// <summary>
        /// HMAC 署名付きトークンを Base64 文字列で生成する。
        /// HTTP レスポンスでの送受信に使用する。
        /// </summary>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="sessionName">Fusion セッション名（SessionName）</param>
        /// <returns>Base64 エンコードされた署名付きトークン文字列</returns>
        public static string CreateToken(byte[] secretKey, string userId, string sessionName)
            => Convert.ToBase64String(CreateTokenBytes(secretKey, userId, sessionName));

        /// <summary>
        /// バイナリトークンの HMAC 署名を検証し、ペイロードを返す。
        /// Dedicated Server の ConnectionToken 検証に使用する（Valkey 不要）。
        /// </summary>
        /// <param name="token">検証するトークンのバイト列</param>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <returns>検証成功時はパース結果、失敗時は null</returns>
        public static SessionTokenParseResult ParseAndVerifyBytes(byte[] token, byte[] secretKey)
        {
            if (token == null || token.Length <= SignatureSize)
            {
                return null;
            }

            var payloadLength = token.Length - SignatureSize;
            var payloadBytes = new byte[payloadLength];
            var signature = new byte[SignatureSize];
            Buffer.BlockCopy(token, 0, payloadBytes, 0, payloadLength);
            Buffer.BlockCopy(token, payloadLength, signature, 0, SignatureSize);

            var expected = HmacRequestSigner.ComputeSignatureBytes(secretKey, payloadBytes);
            if (!HmacRequestSigner.CryptographicEquals(expected, signature))
            {
                return null;
            }

            var (userId, sessionName, timestamp) = UnpackPayload(payloadBytes);
            if (userId == null)
            {
                return null;
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            if (DateTimeOffset.UtcNow - issuedAt > DefaultExpiry)
            {
                return null;
            }

            return new SessionTokenParseResult
            {
                UserId = userId!,
                SessionName = sessionName!,
                IssuedAt = issuedAt,
            };
        }

        /// <summary>
        /// Base64 文字列トークンの HMAC 署名を検証し、ペイロードを返す。
        /// サーバー側の Valkey 失効チェックで使用する。
        /// </summary>
        /// <param name="token">Base64 エンコードされたトークン文字列</param>
        /// <param name="secretKey">HMAC シークレットキー</param>
        /// <returns>検証成功時はパース結果、失敗時は null</returns>
        public static SessionTokenParseResult ParseAndVerify(string token, byte[] secretKey)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            try
            {
                return ParseAndVerifyBytes(Convert.FromBase64String(token), secretKey);
            }
            catch
            {
                return null;
            }
        }

        // =========================================================
        //  MessagePack 手動パック（Source Generator 不要）
        // =========================================================

        /// <summary>
        /// ペイロードを MessagePack array(3) 形式でパックする。
        /// </summary>
        private static byte[] PackPayload(string userId, string sessionName, long timestamp)
        {
            var buffer = new ArrayBufferWriter<byte>(128);
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(3);
            writer.Write(userId);
            writer.Write(sessionName);
            writer.Write(timestamp);
            writer.Flush();
            return buffer.WrittenMemory.ToArray();
        }

        /// <summary>
        /// MessagePack array(3) 形式のペイロードをアンパックする。
        /// パース失敗時は userId / sessionName が null のタプルを返す。
        /// </summary>
        private static (string? userId, string? sessionName, long timestamp) UnpackPayload(byte[] data)
        {
            try
            {
                var reader = new MessagePackReader(data);
                var count = reader.ReadArrayHeader();
                if (count != 3)
                {
                    return (null, null, 0);
                }

                var userId = reader.ReadString();
                var sessionName = reader.ReadString();
                var timestamp = reader.ReadInt64();
                return (userId, sessionName, timestamp);
            }
            catch
            {
                return (null, null, 0);
            }
        }
    }
}
