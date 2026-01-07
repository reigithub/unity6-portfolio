using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Contents.Player;
using Game.Contents.UI;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.Services;
using Game.Core.MessagePipe;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// ゲーム全体に関わるオブジェクトを管理する
    /// </summary>
    public class GameCommonObjects : MonoBehaviour
    {
        private const string Address = "GameCommonObjects";

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Instance.GetService<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
                throw new NullReferenceException($"Load Asset Failed. {Address}");

            var go = Instantiate(prefab);
            if (go.TryGetComponent<GameCommonObjects>(out var commonObjects))
            {
                DontDestroyOnLoad(go);
                commonObjects.Initialize();
            }
            else
            {
                go.SafeDestroy();
                throw new MissingComponentException($"{nameof(GameCommonObjects)} is missing.");
            }
        }

        [SerializeField] private GameObject _mainCamera;
        [SerializeField] private GameObject _directionalLight;
        [SerializeField] private Skybox _skybox;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;

        [SerializeField] private GameUIController _gameUIController;

        [SerializeField] private Image _fadeImage;

        private GameServiceReference<AudioService> _audioService;
        private AudioService AudioService => _audioService.Reference;

        private GameServiceReference<GameSceneService> _sceneService;
        private GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

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
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.System.TimeScale, handler: (status, _) =>
                {
                    Time.timeScale = status ? 1f : 0f;
                    return UniTask.CompletedTask;
                })
                .AddTo(this);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.System.Cursor, handler: (status, _) =>
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
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.System.DirectionalLight, handler: status =>
                {
                    if (_directionalLight) _directionalLight.SetActive(status);
                })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, Material>()
                .Subscribe(MessageKey.System.Skybox, handler: material =>
                {
                    if (_skybox) _skybox.material = material;
                })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.System.DefaultSkybox, handler: _ =>
                {
                    if (_skybox) _skybox.material = _defaultSkyboxMaterial;
                })
                .AddTo(this);

            // Game
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.Game.Ready, handler: async (_, token) => { await AudioService.PlayRandomOneAsync(AudioPlayTag.GameReady, token); })
                .AddTo(this);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.Game.Start, handler: async (_, token) =>
                {
                    AudioService.StopBgm();
                    await AudioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);
                })
                .AddTo(this);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.Game.Quit, handler: async (_, token) =>
                {
                    AudioService.StopBgm();
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.GameQuit, token);
                    GameManager.Instance.GameQuit();
                })
                .AddTo(this);

            // GameScene
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameScene.TransitionEnter, handler: async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(1f, 0.5f, tcs);
                    await tcs.Task;
                })
                .AddTo(this);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameScene.TransitionFinish, handler: async (_, _) =>
                {
                    var tcs = new UniTaskCompletionSource<bool>();
                    DoFade(0f, 1f, tcs);
                    await tcs.Task;
                })
                .AddTo(this);

            // GameStage
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStageService.Startup, _ => { GameServiceManager.Instance.StartupService<GameStageService>(); })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStageService.Shutdown, _ => { GameServiceManager.Instance.ShutdownService<GameStageService>(); })
                .AddTo(this);

            // Player
            GlobalMessageBroker.GetSubscriber<int, GameObject>()
                .Subscribe(MessageKey.Player.SpawnPlayer, handler: player =>
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
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.UI.Escape, handler: escape => { GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.Pause, escape); })
                .AddTo(this);

            GlobalMessageBroker.GetSubscriber<int, Vector2>()
                .Subscribe(MessageKey.UI.ScrollWheel, handler: scrollWheel => { _playerFollowCameraController.SetCameraRadius(scrollWheel); })
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