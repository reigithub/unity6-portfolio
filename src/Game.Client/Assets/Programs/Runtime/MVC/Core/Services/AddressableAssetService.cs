using Game.Shared.Services;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC用Addressablesアセット読み込みサービス
    /// GameServiceManager経由で使用
    /// </summary>
    public class AddressableAssetService : AddressableAssetServiceBase, IGameService
    {
        public void Startup()
        {
        }

        public void Shutdown()
        {
        }
    }
}
