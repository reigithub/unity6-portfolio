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