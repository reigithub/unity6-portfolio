using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    public class AddressableAssetService : GameService
    {
        // Debug.Log(Addressables.BuildPath);
        // Debug.Log(Addressables.PlayerBuildDataPath);
        // Debug.Log(Addressables.RuntimePath);

        public async Task<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            ThrowExceptionIfNullAddress(address);
            return await Addressables.LoadAssetAsync<T>(address);
        }

        public async Task<GameObject> InstantiateAsync(string address, Transform parent = null)
        {
            ThrowExceptionIfNullAddress(address);
            return await Addressables.InstantiateAsync(address, parent);
        }

        public async Task<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            ThrowExceptionIfNullAddress(sceneName);
            return await Addressables.LoadSceneAsync(sceneName, loadSceneMode, activateOnLoad);
        }

        public async Task UnloadSceneAsync(SceneInstance sceneInstance)
        {
            var handle = Addressables.UnloadSceneAsync(sceneInstance);
            await handle;

            // Memo: 何かしらのハンドリングをするなら...
            // if (handle.Status == AsyncOperationStatus.Succeeded)
            // {
            // }
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