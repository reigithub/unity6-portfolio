using System;
using System.Collections.Generic;
using System.Text;
using Game.Library.Shared.RequestSigning;

namespace Game.Shared.Services.Network
{
    public interface IRequestSigningService
    {
        Dictionary<string, string> CreateSignatureHeaders(string method, string path, byte[] bodyBytes);
    }

    public class RequestSigningService : IRequestSigningService
    {
        private readonly byte[] _secretKey;

        public RequestSigningService(byte[] secretKey)
        {
            _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        }

        /// <summary>
        /// リクエストに付与する署名ヘッダーを一括生成
        /// </summary>
        public Dictionary<string, string> CreateSignatureHeaders(string method, string path, byte[] bodyBytes)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nonce = Guid.NewGuid().ToString();

            var canonicalString = HmacRequestSigner.BuildCanonicalString(
                method, path, timestamp, nonce, bodyBytes);

            var signature = HmacRequestSigner.ComputeSignature(_secretKey, canonicalString);

            return new Dictionary<string, string>
            {
                { RequestSigningConstants.SignatureHeader, signature },
                { RequestSigningConstants.TimestampHeader, timestamp.ToString() },
                { RequestSigningConstants.NonceHeader, nonce },
            };
        }
    }
}
