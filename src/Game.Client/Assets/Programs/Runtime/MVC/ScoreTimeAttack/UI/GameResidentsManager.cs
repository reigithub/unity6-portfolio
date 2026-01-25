using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.ScoreTimeAttack.Player;
using Game.Shared.Extensions;
using Game.Shared.Input;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// ゲーム全体に関わるオブジェクトを管理する
    /// </summary>
    public class GameResidentsManager : MonoBehaviour
    {
        private const string Address = "GameResidentsManager";

        private static GameObject _instance;

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
                throw new NullReferenceException($"Load Asset Failed. {Address}");

            var go = Instantiate(prefab);
            if (go.TryGetComponent<GameResidentsManager>(out var commonObjects))
            {
                _instance = go;
                DontDestroyOnLoad(go);
                commonObjects.Initialize();
            }
            else
            {
                go.SafeDestroy();
                throw new MissingComponentException($"{nameof(GameResidentsManager)} is missing.");
            }
        }

        public static async UniTask UnloadAsync()
        {
            _instance.SafeDestroy();
            await UniTask.Yield();
        }

        [SerializeField] private GameObject _mainCamera;
        [SerializeField] private GameObject _directionalLight;
        [SerializeField] private Skybox _skybox;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;

        [SerializeField] private Image _fadeImage;

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private ProjectDefaultInputSystem _inputSystem;
        private ProjectDefaultInputSystem.UIActions _ui;

        private Material _defaultSkyboxMaterial;

        private void Initialize()
        {
            _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 1f);
            if (_skybox) _defaultSkyboxMaterial = _skybox.material;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            MessagePipeService.Subscribe<bool>(MessageKey.System.DirectionalLight, status =>
                {
                    if (_directionalLight) _directionalLight.SetActive(status);
                })
                .AddTo(this);
            MessagePipeService.Subscribe<Material>(MessageKey.System.Skybox, material =>
                {
                    if (_skybox) _skybox.material = material;
                })
                .AddTo(this);
            MessagePipeService.Subscribe(MessageKey.System.DefaultSkybox, () =>
                {
                    if (_skybox) _skybox.material = _defaultSkyboxMaterial;
                })
                .AddTo(this);

            // GameScene
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.TransitionEnter, async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(1f, 0.5f, tcs);
                    await tcs.Task;
                })
                .AddTo(this);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameScene.TransitionFinish, async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(0f, 1f, tcs);
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

                    _playerFollowCameraController.SetPlayer(player);
                })
                .AddTo(this);

            // InputSystem
            MessagePipeService.Subscribe<bool>(MessageKey.InputSystem.Escape, status =>
                {
                    if (status)
                        _ui.Escape.Enable();
                    else
                        _ui.Escape.Disable();
                })
                .AddTo(this);
            MessagePipeService.Subscribe<bool>(MessageKey.InputSystem.ScrollWheel, status =>
                {
                    if (status)
                        _ui.ScrollWheel.Enable();
                    else
                        _ui.ScrollWheel.Disable();
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
                Debug.LogWarning($"[GameResidentsManager] Fade animation failed: {ex.Message}");
                tcs.TrySetCanceled();
            }
        }

        #region InputSystem

        private void Awake()
        {
            _inputSystem = new ProjectDefaultInputSystem();
            _ui = _inputSystem.UI;
        }

        private void OnEnable()
        {
            _inputSystem.Enable();
            _ui.Enable();
        }

        private void OnDisable()
        {
            _inputSystem.Disable();
            _ui.Disable();
        }

        private void OnDestroy()
        {
            _inputSystem.Dispose();
        }

        private void Update()
        {
            // _ui.{InputAction}.IsPressed() //押す～離す間
            // _ui.{InputAction}.WasPressedThisFrame() //押した瞬間
            // _ui.{InputAction}.WasReleasedThisFrame() //離した瞬間

            if (_ui.Escape.WasPressedThisFrame())
            {
                MessagePipeService.PublishForget(MessageKey.UI.Escape);
            }

            if (_ui.ScrollWheel.WasPressedThisFrame())
            {
                // 今はプレイヤーフォローカメラ操作用
                var scrollWheel = _ui.ScrollWheel.ReadValue<Vector2>().normalized;
                _playerFollowCameraController.SetCameraRadius(scrollWheel);
            }
        }

        #endregion
    }
}