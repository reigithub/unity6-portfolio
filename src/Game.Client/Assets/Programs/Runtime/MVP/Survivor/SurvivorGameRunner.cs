using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.Root;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.Shared;
using Game.Shared.SaveData;
using Game.Shared.Chat.Client;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Queue;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivorゲームのエントリポイント
    /// VContainerから依存性を注入され、ゲームの起動・終了を管理
    /// MVC依存なし、純粋なMVPシステムのみを使用
    /// </summary>
    public class SurvivorGameRunner : ISurvivorGameRunner
    {
        private readonly IObjectResolver _container;
        private readonly IGameSceneService _sceneService;
        private readonly IAddressableAssetService _assetService;
        private readonly IMasterDataService _masterDataService;
        private readonly IAudioService _audioService;
        private readonly IInputService _inputService;
        private readonly ISurvivorSaveService _saveService;
        private readonly IAudioSaveService _audioSaveService;
        private readonly IPersistentObjectProvider _persistentObjectProvider;
        private readonly IAuthSessionService _authSessionService;
        private readonly IApiClient _apiClient;
        private readonly IAuthApiService _authApiService;
        private readonly IRequestQueue _requestQueue;
        private readonly INetworkService _networkService;
        private readonly IChatClient _chatClient;
        private readonly IMatchmakingClient _matchmakingClient;
        private readonly ILobbyClient _lobbyClient;

        private GameObject _gameRootInstance;
        private SurvivorGameRootController _gameRootController;
        private IDisposable _queueProcessingSubscription;

        public SurvivorGameRunner(
            IObjectResolver container,
            IGameSceneService sceneService,
            IAddressableAssetService assetService,
            IMasterDataService masterDataService,
            IAudioService audioService,
            IInputService inputService,
            ISurvivorSaveService saveService,
            IAudioSaveService audioSaveService,
            IPersistentObjectProvider persistentObjectProvider,
            IAuthSessionService authSessionService,
            IApiClient apiClient,
            IAuthApiService authApiService,
            IRequestQueue requestQueue,
            INetworkService networkService,
            IChatClient chatClient,
            IMatchmakingClient matchmakingClient,
            ILobbyClient lobbyClient)
        {
            _container = container;
            _sceneService = sceneService;
            _assetService = assetService;
            _masterDataService = masterDataService;
            _audioService = audioService;
            _inputService = inputService;
            _saveService = saveService;
            _audioSaveService = audioSaveService;
            _persistentObjectProvider = persistentObjectProvider;
            _authSessionService = authSessionService;
            _apiClient = apiClient;
            _authApiService = authApiService;
            _requestQueue = requestQueue;
            _networkService = networkService;
            _chatClient = chatClient;
            _matchmakingClient = matchmakingClient;
            _lobbyClient = lobbyClient;
        }

        public async UniTask StartupAsync()
        {
            // 1. サービス起動
            _audioService.Startup();
            _inputService.Startup();

            // 2. マスターデータ読み込み
            await _masterDataService.LoadMasterDataAsync();

            // 3. セーブデータ読み込み
            await _saveService.LoadAsync();
            await _audioSaveService.LoadAsync();

            // 4. セッション復元とリフレッシュ
            if (await _authSessionService.RestoreSessionAsync())
            {
                // Signing key は userId から決定的に導出されるため、refresh 前後で不変。
                // HMAC 署名計算が可能な状態で refresh リクエストを送れるよう先に設定する。
                if (!string.IsNullOrEmpty(_authSessionService.SigningKey))
                {
                    _apiClient.SetSigningKey(_authSessionService.SigningKey);
                }

                // AuthToken は意図的に SetAuthToken しない。
                // 永続化された JWT は既に期限切れの可能性があるため、refresh で新 token を取得した
                // 時点で AuthApiService.OnLoginSuccessAsync が自動で SetAuthToken を実行する。
                // refresh が失敗しても起動は継続し、TitleScene.EnsureValidSessionAsync で再認証する。
                await TryRefreshSessionAsync();
            }

            // 5. ChatClient SignalR 接続設定
            var envConfig = GameEnvironmentHelper.CurrentConfig;
            if (!string.IsNullOrEmpty(envConfig?.WebSocketUrl))
            {
                _chatClient.Configure(
                    envConfig.WebSocketUrl,
                    () => System.Threading.Tasks.Task.FromResult(_authSessionService.AuthToken ?? ""));
            }

            // 6. 共通オブジェクト読み込み（カメラ、UIルートなど）
            await LoadGameRootControllerAsync();

            // 7. リクエストキューの自動処理を設定
            SetupQueueProcessing();

            // 8. 初期シーンへ遷移
            await _sceneService.TransitionAsync<SurvivorTitleScene>();

            Debug.Log("[SurvivorGameRunner] Game started");
        }

        /// <summary>
        /// オンライン復帰時にリクエストキューを自動処理する設定
        /// </summary>
        private void SetupQueueProcessing()
        {
            _queueProcessingSubscription = _networkService.OnConnectivityChanged
                .Where(connected => connected && _requestQueue.PendingCount > 0)
                .Subscribe(_ =>
                {
                    Debug.Log($"[SurvivorGameRunner] Network reconnected, processing {_requestQueue.PendingCount} queued requests");
                    _requestQueue.ProcessQueueAsync().Forget();
                });
        }

        private async UniTask LoadGameRootControllerAsync()
        {
            var prefab = await _assetService.LoadAssetAsync<GameObject>("SurvivorGameRootController");
            if (prefab == null)
            {
                Debug.LogError("[SurvivorGameRunner] Failed to load SurvivorGameRootController prefab");
                return;
            }

            _gameRootInstance = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(_gameRootInstance);

            // VContainerで依存性を注入
            _container.InjectGameObject(_gameRootInstance);

            // コントローラーを取得して初期化
            if (_gameRootInstance.TryGetComponent(out _gameRootController))
            {
                _gameRootController.Initialize();

                // 永続オブジェクトとして登録
                _persistentObjectProvider.Register<IGameRootController>(_gameRootController);
            }
            else
            {
                Debug.LogError("[SurvivorGameRunner] SurvivorGameRootController component not found");
            }
        }

        /// <summary>
        /// 起動時に保存された refresh token で session を再開する。
        /// Refresh に成功した場合は <see cref="Game.Shared.Services.AuthApiService"/>.OnLoginSuccessAsync が
        /// 自動で <see cref="IApiClient.SetAuthToken"/> + <see cref="IApiClient.SetSigningKey"/> を実行する。
        /// 失敗しても起動は継続し、TitleScene.EnsureValidSessionAsync で再認証を行う (recovery 責務は TitleScene 側)。
        /// </summary>
        private async UniTask TryRefreshSessionAsync()
        {
            try
            {
                var refreshResult = await _authApiService.RefreshTokenAsync();
                if (refreshResult.IsSuccess)
                {
                    Debug.Log("[SurvivorGameRunner] Session restored via refresh token");
                }
                else
                {
                    // Refresh token 自体が無効 (30 日期限切れ、DB から削除、etc)。
                    // Client の _apiClient._authToken は null のまま。TitleScene で GuestLogin fallback に進む。
                    Debug.LogWarning(
                        $"[SurvivorGameRunner] Refresh token invalid: {refreshResult.Error?.Message}. " +
                        "Will re-authenticate on title screen.");
                }
            }
            catch (System.Exception e)
            {
                // 例外発生時もログのみ
                Debug.LogWarning($"[SurvivorGameRunner] Session refresh error: {e.Message}");
            }
        }


        public async UniTask ShutdownAsync()
        {
            // キュー処理サブスクリプションを解除
            _queueProcessingSubscription?.Dispose();
            _queueProcessingSubscription = null;

            // クライアント切断
            if (_chatClient != null) { await _chatClient.DisconnectAsync(); }
            if (_matchmakingClient != null) { await _matchmakingClient.DisconnectAsync(); }
            if (_lobbyClient != null) { await _lobbyClient.DisconnectAsync(); }

            // セーブデータ保存（変更がある場合のみ）
            await _saveService.SaveIfDirtyAsync();
            await _audioSaveService.SaveIfDirtyAsync();

            // 全てのシーンを終了させる
            await _sceneService.TerminateAllAsync();

            // サービスシャットダウン
            _audioService.Shutdown();
            _inputService.Shutdown();

            // 永続オブジェクトの登録解除
            _persistentObjectProvider.Clear();

            // 共通オブジェクト破棄
            if (_gameRootInstance != null)
            {
                UnityEngine.Object.Destroy(_gameRootInstance);
                _gameRootInstance = null;
            }

            await UniTask.Yield();
            Debug.Log("[SurvivorGameRunner] Game shutdown");
        }
    }
}
