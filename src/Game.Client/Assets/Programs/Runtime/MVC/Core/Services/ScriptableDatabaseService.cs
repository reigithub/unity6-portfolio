using Cysharp.Threading.Tasks;
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
        private readonly IAddressableAssetService _assetService;

        public ScriptableDatabaseService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        protected override async UniTask<ScriptableDatabase> LoadDatabaseAssetAsync()
        {
            return await _assetService.LoadAssetAsync<ScriptableDatabase>("ScriptableDatabase");
        }
    }
}
