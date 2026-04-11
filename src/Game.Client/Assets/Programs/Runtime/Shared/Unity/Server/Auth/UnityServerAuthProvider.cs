using Game.Library.Shared.RequestSigning;
using Game.Shared.Network.Fusion;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Survivor ゲーム用の SessionToken を検証する <see cref="IUnityServerAuthProvider"/> 実装。
    /// ConnectionToken（MessagePack + HMAC-SHA256 バイナリ形式）を直接検証する。
    /// </summary>
    public sealed class UnityServerAuthProvider : IUnityServerAuthProvider
    {
        private readonly byte[] _secretKey;

        /// <summary>
        /// <see cref="UnityServerAuthProvider"/> を初期化する。
        /// </summary>
        /// <param name="secretKey">HMAC 検証用シークレットキー。</param>
        public UnityServerAuthProvider(byte[] secretKey)
        {
            _secretKey = secretKey;
        }

        /// <summary>
        /// ConnectionToken バイト列を MessagePack + HMAC-SHA256 形式で直接検証する。
        /// </summary>
        /// <param name="token">クライアントから送られた ConnectionToken バイト列。</param>
        /// <returns>認証成功なら true、失敗なら false。</returns>
        public bool ValidateConnectionToken(byte[] token)
        {
            if (token == null || token.Length == 0)
                return false;

            return SessionTokenHelper.ParseAndVerifyBytes(token, _secretKey) != null;
        }
    }
}
