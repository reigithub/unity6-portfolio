using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Shared.Services
{
    /// <summary>
    /// Addressablesアセット読み込みサービスの共通インターフェース
    /// MVC/MVP両方で使用
    /// </summary>
    public interface IAddressableAssetService
    {
        UniTask<T> LoadAssetAsync<T>(string address) where T : Object;
        UniTask<GameObject> InstantiateAsync(string address, Transform parent = null);
        UniTask<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true);
        UniTask UnloadSceneAsync(SceneInstance sceneInstance);
    }
}
