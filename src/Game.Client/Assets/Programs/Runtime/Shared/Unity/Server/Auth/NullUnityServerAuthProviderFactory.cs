using Game.Shared.Network.Fusion;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// クライアント向け <see cref="IUnityServerAuthProviderFactory"/> の Null Object 実装。
    /// <see cref="Create"/> は常に null を返し、認証が不要なクライアント側での DI 解決に使用する。
    /// </summary>
    public sealed class NullUnityServerAuthProviderFactory : IUnityServerAuthProviderFactory
    {
        /// <inheritdoc/>
        /// <returns>常に null を返す。</returns>
        public IUnityServerAuthProvider Create() => null;
    }
}
