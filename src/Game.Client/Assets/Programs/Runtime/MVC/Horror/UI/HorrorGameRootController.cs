using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Horror
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
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
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
            _instance.SafeDestroy();
            await UniTask.Yield();
        }

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Initialize()
        {
            // playerInput.controlsChangedEvent.AddListener(UpdateControlScheme);
            // InputSystem.onEvent += (inputEventPtr, device) => { Debug.Log($"InputSystem InputDevice: {device}"); };
            // Keyboard.current / Mouse.current / Gamepad.current / Pointer.current / Touchscreen.current;
            // playerInput.SwitchCurrentControlScheme(InputConstants.Gamepad);

            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            _playerInput.controlsChangedEvent.AsObservable()
                .Subscribe(x => inputService.UpdateControlScheme(x.currentControlScheme))
                .AddTo(this);

            // GameScene
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            messagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.FadeOut, async (_, token) =>
                {
                    await _canvasGroup.DOFade(UIAnimationConstants.AlphaOpaque, UIAnimationConstants.SceneTransitionFadeInDuration)
                        .SetUpdate(true)
                        .ToUniTask(cancellationToken: token);
                })
                .AddTo(this);
            messagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.FadeIn, async (_, token) =>
                {
                    await _canvasGroup.DOFade(UIAnimationConstants.AlphaTransparent, UIAnimationConstants.SceneTransitionFadeOutDuration)
                        .SetUpdate(true)
                        .ToUniTask(cancellationToken: token);
                })
                .AddTo(this);
        }
    }
}
