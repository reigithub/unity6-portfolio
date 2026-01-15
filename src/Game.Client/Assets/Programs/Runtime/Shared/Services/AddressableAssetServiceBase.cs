using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Shared.Services
{
    /// <summary>
    /// Addressablesアセット読み込みサービスの共通基底クラス
    /// MVC/MVP両方で継承して使用
    /// </summary>
    public abstract class AddressableAssetServiceBase : IAddressableAssetService
    {
        public async UniTask<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            ThrowExceptionIfNullAddress(address);
            return await Addressables.LoadAssetAsync<T>(address);
        }

        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent = null)
        {
            ThrowExceptionIfNullAddress(address);
            return await Addressables.InstantiateAsync(address, parent);
        }

        public async UniTask<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            ThrowExceptionIfNullAddress(sceneName);
            return await Addressables.LoadSceneAsync(sceneName, loadSceneMode, activateOnLoad);
        }

        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance)
        {
            await Addressables.UnloadSceneAsync(sceneInstance);
        }

        private void ThrowExceptionIfNullAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new InvalidOperationException("Address is Null.");
            }
        }
    }
}
