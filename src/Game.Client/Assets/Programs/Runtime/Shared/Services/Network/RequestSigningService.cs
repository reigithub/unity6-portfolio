using System;
using System.Collections.Generic;
using Game.Library.Shared.RequestSigning;

namespace Game.Shared.Services.Network
{
    public interface IRequestSigningService
    {
        Dictionary<string, string> CreateSignatureHeaders(string method, string path, byte[] bodyBytes);
        void SetKey(byte[] secretKey);
        bool HasKey { get; }
    }

    public class RequestSigningService : IRequestSigningService
    {
        private byte[] _secretKey;

        public RequestSigningService() { }

        public bool HasKey => _secretKey != null && _secretKey.Length > 0;

        public void SetKey(byte[] secretKey)
        {
            _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        }

        /// <summary>
        /// リクエストに付与する署名ヘッダーを一括生成
        /// </summary>
        public Dictionary<string, string> CreateSignatureHeaders(string method, string path, byte[] bodyBytes)
        {
            if (_secretKey == null || _secretKey.Length == 0)
                return new Dictionary<string, string>();

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
