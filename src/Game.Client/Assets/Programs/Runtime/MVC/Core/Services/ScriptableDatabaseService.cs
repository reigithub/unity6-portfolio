using Cysharp.Threading.Tasks;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;
using UnityEngine.AddressableAssets;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC 用 ScriptableDatabase ロードサービス。GameServiceManager 経由で使用。
    /// MVC <c>MasterDataService</c> と同じく Addressables からコンテナ資産をロードする。
    /// </summary>
    public class ScriptableDatabaseService : ScriptableDatabaseServiceBase, IGameService
    {
        public ScriptableDatabaseService()
        {
        }

        public void Startup()
        {
        }

        public void Shutdown()
        {
        }

        protected override async UniTask<ScriptableDatabase> LoadDatabaseAssetAsync()
        {
            return await Addressables.LoadAssetAsync<ScriptableDatabase>("ScriptableDatabase");
        }
    }
}
