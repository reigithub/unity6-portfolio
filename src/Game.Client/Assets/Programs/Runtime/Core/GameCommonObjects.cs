using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Contents.Player;
using Game.Contents.UI;
using Game.Core.Extensions;
using Game.Core.Services;
using Game.Core.MessagePipe;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    public class GameCommonObjects : MonoBehaviour
    {
        private const string Address = "Assets/Prefabs/GameCommonObjects.prefab";

        public static GameCommonObjects Instance { get; private set; }

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Instance.GetService<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
            {
                Debug.LogError($"Load Asset Failed. {Address}");
                Debug.Break();
            }

            Instance.SafeDestroy();
            Instance = null;

            var go = Instantiate(prefab);
            if (go.TryGetComponent<GameCommonObjects>(out var commonObjects))
            {
                DontDestroyOnLoad(go);
                commonObjects.Initialize();
                Instance = commonObjects;
            }
        }

        [SerializeField] private GameObject _mainCamera;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;

        [SerializeField] private GameUIController _gameUIController;

        [SerializeField] private Image _fadeImage;

        private GameServiceReference<GameSceneService> _sceneService;
        private GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        // Memo: どこに持つかは要検討としてマーク
        private bool _gameStart;

        private void Initialize()
        {
            _gameUIController.Initialize();
            _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 1f);
            Subscribe();
        }

        private void Subscribe()
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

            // Game
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.Game.Start, handler: _ => { _gameStart = true; })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.Game.Quit, handler: _ => { GameManager.Instance.GameQuit(); })
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
            GlobalMessageBroker.GetSubscriber<int, Vector2>()
                .Subscribe(MessageKey.UI.ScrollWheel, handler: scrollWheel =>
                {
                    if (!_gameStart) return;
                    _playerFollowCameraController.SetCameraRadius(scrollWheel);
                })
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