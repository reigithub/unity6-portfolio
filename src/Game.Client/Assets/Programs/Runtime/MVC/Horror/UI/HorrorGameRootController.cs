using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// ゲーム全体に関わるオブジェクトを管理する
    /// </summary>
    public class HorrorGameRootController : MonoBehaviour
    {
        private const string Address = "HorrorGameRootController";

        private static HorrorGameRootController _instance;

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
                throw new NullReferenceException($"Load Asset Failed. {Address}");

            var go = Instantiate(prefab);
            if (go.TryGetComponent<HorrorGameRootController>(out var gameRootController))
            {
                _instance = gameRootController;
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
            _instance = null;
            await UniTask.Yield();
        }

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private Image _fadeImage;

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private void Initialize()
        {
            // playerInput.controlsChangedEvent.AddListener(UpdateControlScheme);
            // InputSystem.onEvent += (inputEventPtr, device) => { Debug.Log($"InputSystem InputDevice: {device}"); };
            // Keyboard.current / Mouse.current / Gamepad.current / Pointer.current / Touchscreen.current;
            // playerInput.SwitchCurrentControlScheme(InputConstants.Gamepad);
            _playerInput.controlsChangedEvent.AsObservable()
                .Subscribe(x => InputService.UpdateControlScheme(x.currentControlScheme))
                .AddTo(this);

            // GameScene
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.FadeOut, async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(UIAnimationConstants.AlphaOpaque, UIAnimationConstants.SceneTransitionFadeInDuration, tcs);
                    await tcs.Task;
                })
                .AddTo(this);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.FadeIn, async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(UIAnimationConstants.AlphaTransparent, UIAnimationConstants.SceneTransitionFadeOutDuration, tcs);
                    await tcs.Task;
                })
                .AddTo(this);
        }

        private void DoFade(float endValue, float duration, UniTaskCompletionSource<bool> tcs)
        {
            try
            {
                _fadeImage.DOFade(endValue, duration).SetUpdate(true)
                    .onComplete += () => { tcs.TrySetResult(true); };
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameRootController] Fade animation failed: {ex.Message}");
                tcs.TrySetCanceled();
            }
        }
    }
}
