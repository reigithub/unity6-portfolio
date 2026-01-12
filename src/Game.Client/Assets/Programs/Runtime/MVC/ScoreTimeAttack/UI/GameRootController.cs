using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ScoreTimeAttack.Player;
using Game.Contents.UI;
using Game.Core.Extensions;
using Game.Core.Services;
using Game.Core.MessagePipe;
using Game.Library.Shared.Enums;
using Game.ScoreTimeAttack.Services;
using Game.Shared.Bootstrap;
using R3;
using UnityEngine;
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
            if (go.TryGetComponent<GameRootController>(out var commonObjects))
            {
                _instance = go;
                DontDestroyOnLoad(go);
                commonObjects.Initialize();
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

        [SerializeField] private GameObject _mainCamera;
        [SerializeField] private GameObject _directionalLight;
        [SerializeField] private Skybox _skybox;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;

        [SerializeField] private GameUIController _gameUIController;

        [SerializeField] private Image _fadeImage;

        private IAudioService _audioService;
        private IAudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private Material _defaultSkyboxMaterial;

        private void Initialize()
        {
            _gameUIController.Initialize();
            _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 1f);
            if (_skybox) _defaultSkyboxMaterial = _skybox.material;
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            MessagePipeService.SubscribeAsync<bool>(MessageKey.System.TimeScale, (status, _) =>
                {
                    Time.timeScale = status ? 1f : 0f;
                    return UniTask.CompletedTask;
                })
                .AddTo(this);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.System.Cursor, (status, _) =>
                {
                    if (status)
                    {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    }

                    return UniTask.CompletedTask;
                })
                .AddTo(this);
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

            // Game
            MessagePipeService.SubscribeAsync<bool>(MessageKey.Game.Ready, async (_, token) => { await AudioService.PlayRandomOneAsync(AudioPlayTag.GameReady, token); })
                .AddTo(this);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.Game.Start, async (_, token) =>
                {
                    AudioService.StopBgm();
                    await AudioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);
                })
                .AddTo(this);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.Game.Quit, async (_, token) =>
                {
                    AudioService.StopBgm();
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.GameQuit, token);
                    ApplicationEvents.RequestShutdown();
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

            // GameStage
            MessagePipeService.Subscribe(MessageKey.GameStageService.Startup, () => { GameServiceManager.Instance.Startup<ScoreTimeAttackStageService>(); })
                .AddTo(this);
            MessagePipeService.Subscribe(MessageKey.GameStageService.Shutdown, () => { GameServiceManager.Instance.Shutdown<ScoreTimeAttackStageService>(); })
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

            // UI
            MessagePipeService.Subscribe<bool>(MessageKey.UI.Escape, escape => { MessagePipeService.PublishForget(MessageKey.GameStage.Pause, escape); })
                .AddTo(this);

            MessagePipeService.Subscribe<Vector2>(MessageKey.UI.ScrollWheel, scrollWheel => { _playerFollowCameraController.SetCameraRadius(scrollWheel); })
                .AddTo(this);
        }

        private void DoFade(float endValue, float duration, UniTaskCompletionSource<bool> tcs)
        {
            try
            {
                _fadeImage.DOFade(endValue, duration)
                    .onComplete += () => { tcs.TrySetResult(true); };
            }
            catch (Exception)
            {
                tcs.TrySetCanceled();
            }
        }
    }
}