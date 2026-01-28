using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.App.Title
{
    /// <summary>
    /// アプリレベルのシーンローダー
    /// タイトル画面専用の軽量実装
    /// </summary>
    public class AppSceneLoader
    {
        private GameObject _currentInstance;

        public async UniTask<T> LoadAsync<T>(string address) where T : Component
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new GameAssetLoadException(address, typeof(T), "Address is null or empty");
            }

            // 前のシーンを破棄
            Unload();

            try
            {
                // Addressablesから読み込み
                var handle = Addressables.LoadAssetAsync<GameObject>(address);
                var prefab = await handle.ToUniTask();

                if (prefab == null)
                {
                    throw new GameAssetLoadException(address, typeof(GameObject), $"Prefab loaded but returned null: {address}");
                }

                _currentInstance = UnityEngine.Object.Instantiate(prefab);

                if (_currentInstance == null)
                {
                    throw new GameAssetLoadException(address, typeof(GameObject), $"Instantiate returned null: {address}");
                }

                if (!_currentInstance.TryGetComponent<T>(out var component))
                {
                    UnityEngine.Object.Destroy(_currentInstance);
                    _currentInstance = null;
                    throw new GameAssetLoadException(address, typeof(T), $"Component {typeof(T).Name} not found on prefab: {address}");
                }

                return component;
            }
            catch (GameAssetLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppSceneLoader] Failed to load {address}: {ex.Message}");
                throw new GameAssetLoadException(address, typeof(T), $"Failed to load prefab: {address}", ex);
            }
        }

        public void Unload()
        {
            if (_currentInstance != null)
            {
                UnityEngine.Object.Destroy(_currentInstance);
                _currentInstance = null;
            }
        }
    }
}
