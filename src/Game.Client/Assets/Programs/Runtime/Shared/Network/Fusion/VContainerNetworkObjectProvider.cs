using Fusion;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// VContainer DI 対応の NetworkObject プロバイダ。
    /// Fusion がプレハブをインスタンス化する際に InjectGameObject を呼び出し、
    /// サーバー・クライアントレプリカ問わず全 NetworkObject に DI 注入する。
    /// これにより各 NetworkBehaviour.Spawned() でのフォールバック DI が不要になる。
    /// </summary>
    public class VContainerNetworkObjectProvider : NetworkObjectProviderDefault
    {
        private IObjectResolver _resolver;

        public void SetResolver(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        protected override NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab)
        {
            var instance = Instantiate(prefab);

            if (_resolver != null)
            {
                _resolver.InjectGameObject(instance.gameObject);
            }
            else
            {
                Debug.LogWarning("[VContainerNetworkObjectProvider] Resolver is null, skipping DI injection");
            }

            return instance;
        }
    }
}
