using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.App.Title
{
    /// <summary>
    /// アプリレベルのシーンローダー
    /// タイトル画面専用の軽量実装
    /// </summary>
    public class AppSceneLoader
    {
        private GameObject _currentInstance;
        private AsyncOperationHandle<GameObject> _currentHandle;

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
                _currentHandle = Addressables.LoadAssetAsync<GameObject>(address);
                var prefab = await _currentHandle.ToUniTask();

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
                    Addressables.Release(_currentHandle);
                    _currentHandle = default;
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
                if (_currentHandle.IsValid())
                {
                    Addressables.Release(_currentHandle);
                    _currentHandle = default;
                }
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

            if (_currentHandle.IsValid())
            {
                Addressables.Release(_currentHandle);
                _currentHandle = default;
            }
        }
    }
}
