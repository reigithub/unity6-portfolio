using VContainer;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="IUnityServerAuthProviderFactory"/> のサーバー用実装。
    /// <see cref="UnityServerConfigProvider"/> から AuthSecretKey を取得し、
    /// <see cref="UnityServerAuthProvider"/> を生成する。
    /// </summary>
    public sealed class UnityServerAuthProviderFactory : IUnityServerAuthProviderFactory
    {
        private readonly UnityServerConfigProvider _configProvider;

        /// <summary>
        /// <see cref="UnityServerAuthProviderFactory"/> を初期化する。
        /// </summary>
        /// <param name="configProvider">設定プロバイダ。</param>
        [Inject]
        public UnityServerAuthProviderFactory(UnityServerConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        /// <inheritdoc/>
        public IUnityServerAuthProvider Create()
        {
            var config = _configProvider.Current;
            if (config.AuthSecretKey.IsEmpty)
                return null;

            // ReadOnlyMemory<byte> → byte[] に変換してプロバイダへ渡す
            var keyBytes = config.AuthSecretKey.ToArray();
            return new UnityServerAuthProvider(keyBytes);
        }
    }
}
