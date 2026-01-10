using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Core.Bootstrap
{
    /// <summary>
    /// VContainerを使用したDI方式の起動
    /// </summary>
    public class VContainerGameLauncher : IGameLauncher
    {
        private LifetimeScope _rootScope;

        public async UniTask StartupAsync()
        {
            // 1. RootLifetimeScopeを生成
            var rootObject = new GameObject("GameRootLifetimeScope");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);

            _rootScope = rootObject.AddComponent<GameRootLifetimeScope>();

            // 2. コンテナ構築完了を待つ（Awakeで構築される）
            await UniTask.Yield();

            // 3. GameInitializerが自動起動（IAsyncStartable）
        }

        public void Shutdown()
        {
            if (_rootScope != null)
            {
                UnityEngine.Object.Destroy(_rootScope.gameObject);
                _rootScope = null;
            }
        }
    }

    public class GameRootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // サービス登録（Singleton）
            builder.Register<IAddressableAssetService, AddressableAssetService>(Lifetime.Singleton);
            builder.Register<IMessageBrokerService, MessageBrokerService>(Lifetime.Singleton);
            builder.Register<IGameStageService, GameStageService>(Lifetime.Singleton);
            builder.Register<IMasterDataService, MasterDataService>(Lifetime.Singleton);
            builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
            builder.Register<IGameSceneService, GameSceneService>(Lifetime.Singleton);

            // エントリーポイント登録
            builder.RegisterEntryPoint<GameInitializer>();
        }
    }

    /// <summary>
    /// VContainer用のゲーム初期化エントリーポイント
    /// </summary>
    public class GameInitializer : IAsyncStartable, IDisposable
    {
        private readonly IMasterDataService _masterDataService;
        private readonly IMessageBrokerService _messageBrokerService;
        private readonly IAudioService _audioService;
        private readonly IGameSceneService _gameSceneService;

        public GameInitializer(
            IMasterDataService masterDataService,
            IMessageBrokerService messageBrokerService,
            IAudioService audioService,
            IGameSceneService gameSceneService
        )
        {
            _masterDataService = masterDataService;
            _messageBrokerService = messageBrokerService;
            _audioService = audioService;
            _gameSceneService = gameSceneService;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("[GameInitializer] Initializing services...");

            // 1. サービス初期化
            _masterDataService.Startup();
            _messageBrokerService.Startup();
            _audioService.Startup();
            _gameSceneService.Startup();

            // 2. 共通オブジェクト読み込み
            await GameRootController.LoadAssetAsync();

            // 3. マスターデータ読み込み
            await _masterDataService.LoadMasterDataAsync();

            // 4. 初期シーン遷移
            await _gameSceneService.TransitionAsync<GameTitleScene>();

            Debug.Log("[GameInitializer] Initialization complete.");
        }

        public void Dispose()
        {
            Debug.Log("[GameInitializer] Disposing...");
            _messageBrokerService.Shutdown();
        }
    }
}