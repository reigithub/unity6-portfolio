using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    /// <summary>
    /// Addressablesアセット読み込みサービスのインターフェース
    /// </summary>
    public interface IAddressableAssetService : IGameService
    {
        Task<T> LoadAssetAsync<T>(string address) where T : Object;
        Task<GameObject> InstantiateAsync(string address, Transform parent = null);
        Task<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true);
        Task UnloadSceneAsync(SceneInstance sceneInstance);
    }
}
