using Cysharp.Threading.Tasks;
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
            // 前のシーンを破棄
            Unload();

            // Addressablesから読み込み
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            var prefab = await handle.ToUniTask();

            if (prefab == null)
            {
                Debug.LogError($"[AppSceneLoader] Failed to load: {address}");
                return null;
            }

            _currentInstance = Object.Instantiate(prefab);
            return _currentInstance.GetComponent<T>();
        }

        public void Unload()
        {
            if (_currentInstance != null)
            {
                Object.Destroy(_currentInstance);
                _currentInstance = null;
            }
        }
    }
}
