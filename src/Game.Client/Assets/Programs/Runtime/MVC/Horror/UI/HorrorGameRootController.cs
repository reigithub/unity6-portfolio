using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// ゲーム全体に関わるオブジェクトを管理する
    /// </summary>
    public class HorrorGameRootController : MonoBehaviour
    {
        private const string Address = "HorrorGameRootController";

        private static GameObject _instance;

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
                throw new NullReferenceException($"Load Asset Failed. {Address}");

            var go = Instantiate(prefab);
            if (go.TryGetComponent<HorrorGameRootController>(out var gameRootController))
            {
                _instance = go;
                DontDestroyOnLoad(go);
                gameRootController.Initialize();
            }
            else
            {
                go.SafeDestroy();
                throw new MissingComponentException($"{nameof(HorrorGameRootController)} is missing.");
            }
        }

        public static async UniTask UnloadAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            assetService.ReleaseAsset(_instance);
            _instance.SafeDestroy();
            await UniTask.Yield();
        }

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerInput _playerInput;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private void Initialize() => InputService.SubscribeControlScheme(_playerInput);
    }
}
