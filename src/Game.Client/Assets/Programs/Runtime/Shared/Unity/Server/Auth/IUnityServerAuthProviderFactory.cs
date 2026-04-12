using Game.Shared.Network.Fusion;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="IUnityServerAuthProvider"/> を生成するファクトリインターフェース。
    /// HMAC シークレット未設定時は null を返すことで認証スキップを表現する。
    /// </summary>
    public interface IUnityServerAuthProviderFactory
    {
        /// <summary>
        /// Auth プロバイダを生成する。
        /// HMAC シークレットが未設定の場合は null を返す（認証スキップ）。
        /// </summary>
        /// <returns>Auth プロバイダ。未設定時は null。</returns>
        IUnityServerAuthProvider Create();
    }
}
