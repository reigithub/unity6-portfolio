namespace Game.Library.Shared.RequestSigning
{
    public static class RequestSigningConstants
    {
        public const string SignatureHeader = "X-Signature";
        public const string TimestampHeader = "X-Timestamp";
        public const string NonceHeader = "X-Nonce";
        public const int TimestampToleranceSeconds = 300; // ±5分
        public const int NonceExpirySeconds = 600;        // 10分
    }
}
