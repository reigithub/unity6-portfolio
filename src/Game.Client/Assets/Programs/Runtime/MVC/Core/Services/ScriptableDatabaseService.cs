using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC 用 ScriptableDatabase ロードサービス。GameServiceManager 経由で使用。
    /// MVC <c>MasterDataService</c> と同じく Addressables からコンテナ資産をロードする。
    /// </summary>
    public class ScriptableDatabaseService : ScriptableDatabaseServiceBase, IGameService
    {
        private const string AssetAddress = "ScriptableDatabase";

        private IAddressableAssetService _assetService;

        public ScriptableDatabaseService()
        {
        }

        public ScriptableDatabaseService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public void Startup()
        {
        }

        public void Shutdown()
        {
        }

        protected override async UniTask<ScriptableDatabase> LoadDatabaseAssetAsync()
        {
            _assetService ??= GameServiceManager.Get<AddressableAssetService>();

            if (_assetService == null)
            {
                throw new DependencyInjectionException(
                    typeof(IAddressableAssetService),
                    DIErrorType.ServiceNotRegistered,
                    "IAddressableAssetService not available in ScriptableDatabaseService");
            }

            return await _assetService.LoadAssetAsync<ScriptableDatabase>(AssetAddress);
        }
    }
}
