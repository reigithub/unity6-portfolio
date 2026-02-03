using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
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
        // Constants
        private const int DefaultMaxRetries = 3;
        private const int DefaultRetryDelayMs = 500;

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

        public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
        {
            if (asset != null)
            {
                Addressables.Release(asset);
            }
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
            }
        }

        private void ThrowExceptionIfNullAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new GameAssetLoadException(address, typeof(object), "Address is null or empty");
            }
        }

        /// <summary>
        /// アセットを安全に読み込む（nullチェック付き）
        /// 読み込み失敗時はGameAssetLoadExceptionをスロー
        /// </summary>
        /// <typeparam name="T">アセットの型</typeparam>
        /// <param name="address">アセットアドレス</param>
        /// <returns>読み込まれたアセット</returns>
        public async UniTask<T> LoadAssetSafeAsync<T>(string address) where T : UnityEngine.Object
        {
            ThrowExceptionIfNullAddress(address);

            try
            {
                var asset = await Addressables.LoadAssetAsync<T>(address);

                if (asset == null)
                {
                    throw new GameAssetLoadException(
                        address,
                        typeof(T),
                        $"Asset loaded but returned null: {address}");
                }

                return asset;
            }
            catch (GameAssetLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new GameAssetLoadException(
                    address,
                    typeof(T),
                    $"Failed to load asset: {address}",
                    ex);
            }
        }

        /// <summary>
        /// リトライ付きでアセットを読み込む
        /// ネットワーク不安定時などに使用
        /// </summary>
        /// <typeparam name="T">アセットの型</typeparam>
        /// <param name="address">アセットアドレス</param>
        /// <param name="maxRetries">最大リトライ回数</param>
        /// <param name="retryDelayMs">リトライ間隔（ミリ秒）</param>
        /// <returns>読み込まれたアセット</returns>
        public async UniTask<T> LoadAssetWithRetryAsync<T>(
            string address,
            int maxRetries = DefaultMaxRetries,
            int retryDelayMs = DefaultRetryDelayMs) where T : UnityEngine.Object
        {
            ThrowExceptionIfNullAddress(address);

            Exception lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var asset = await Addressables.LoadAssetAsync<T>(address);

                    if (asset == null)
                    {
                        throw new GameAssetLoadException(
                            address,
                            typeof(T),
                            $"Asset loaded but returned null: {address}",
                            retryCount: attempt);
                    }

                    if (attempt > 0)
                    {
                        Debug.Log($"[AddressableAsset] Successfully loaded {address} after {attempt} retries");
                    }

                    return asset;
                }
                catch (GameAssetLoadException ex)
                {
                    lastException = ex;
                }
                catch (Exception ex)
                {
                    lastException = new GameAssetLoadException(
                        address,
                        typeof(T),
                        $"Failed to load asset: {address}",
                        ex,
                        attempt);
                }

                if (attempt < maxRetries)
                {
                    Debug.LogWarning($"[AddressableAsset] Attempt {attempt + 1}/{maxRetries + 1} failed for {address}. Retrying...");
                    await UniTask.Delay(retryDelayMs);
                }
            }

            Debug.LogError($"[AddressableAsset] All {maxRetries + 1} attempts failed for {address}");
            throw lastException;
        }

        /// <summary>
        /// アセットを安全にインスタンス化する（nullチェック付き）
        /// </summary>
        /// <param name="address">アセットアドレス</param>
        /// <param name="parent">親Transform</param>
        /// <returns>インスタンス化されたGameObject</returns>
        public async UniTask<GameObject> InstantiateSafeAsync(string address, Transform parent = null)
        {
            ThrowExceptionIfNullAddress(address);

            try
            {
                var instance = await Addressables.InstantiateAsync(address, parent);

                if (instance == null)
                {
                    throw new GameAssetLoadException(
                        address,
                        typeof(GameObject),
                        $"Instantiate returned null: {address}");
                }

                return instance;
            }
            catch (GameAssetLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new GameAssetLoadException(
                    address,
                    typeof(GameObject),
                    $"Failed to instantiate: {address}",
                    ex);
            }
        }

        /// <summary>
        /// アセットを安全にインスタンス化し、指定コンポーネントを取得する
        /// </summary>
        /// <typeparam name="T">取得するコンポーネントの型</typeparam>
        /// <param name="address">アセットアドレス</param>
        /// <param name="parent">親Transform</param>
        /// <returns>コンポーネント</returns>
        public async UniTask<T> InstantiateWithComponentAsync<T>(string address, Transform parent = null) where T : Component
        {
            var instance = await InstantiateSafeAsync(address, parent);

            if (!instance.TryGetComponent<T>(out var component))
            {
                Addressables.ReleaseInstance(instance);
                throw new GameAssetLoadException(
                    address,
                    typeof(T),
                    $"Component {typeof(T).Name} not found on instantiated prefab: {address}");
            }

            return component;
        }
    }
}

