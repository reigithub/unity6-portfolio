using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.ScoreTimeAttack.Player;
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
    public class GameRootController : MonoBehaviour
    {
        private const string Address = "GameRootController";

        private static GameObject _instance;

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
                throw new NullReferenceException($"Load Asset Failed. {Address}");

            var go = Instantiate(prefab);
            if (go.TryGetComponent<GameRootController>(out var gameRootController))
            {
                _instance = go;
                DontDestroyOnLoad(go);
                gameRootController.Initialize();
            }
            else
            {
                go.SafeDestroy();
                throw new MissingComponentException($"{nameof(GameRootController)} is missing.");
            }
        }

        public static async UniTask UnloadAsync()
        {
            _instance.SafeDestroy();
            await UniTask.Yield();
        }

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;
        [SerializeField] private PlayerInput _playerInput;

        [SerializeField] private Image _fadeImage;

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private void Initialize()
        {
            _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, UIAnimationConstants.AlphaOpaque);
            _playerInput.controlsChangedEvent.AsObservable()
                .Subscribe(x => InputService.UpdateControlScheme(x.currentControlScheme))
                .AddTo(this);
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
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

            // Player
            MessagePipeService.Subscribe<GameObject>(MessageKey.Player.SpawnPlayer, player =>
                {
                    // 現在プレイヤーはUnityちゃんしかいない
                    if (player.TryGetComponent<SDUnityChanPlayerController>(out var controller))
                    {
                        controller.SetMainCamera(_mainCamera.transform);
                    }

                    SetFollowTarget(player.transform);
                })
                .AddTo(this);

            // InputSystem
            MessagePipeService.Subscribe<Vector2>(MessageKey.UI.ScrollWheel, radius =>
                {
                    SetCameraRadius(radius);
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
                // 正常なキャンセル
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameRootController] Fade animation failed: {ex.Message}");
                tcs.TrySetCanceled();
            }
        }

        public void SetFollowTarget(Transform target)
        {
            if (_playerFollowCameraController != null && target != null)
                _playerFollowCameraController.SetFollowTarget(target);
        }

        public void ClearFollowTarget()
        {
            if (_playerFollowCameraController != null)
                _playerFollowCameraController.ClearFollowTarget();
        }

        public void SetCameraRadius(Vector2 scrollWheel)
        {
            if (_playerFollowCameraController != null)
                _playerFollowCameraController.SetCameraRadius(scrollWheel);
        }
    }
}
