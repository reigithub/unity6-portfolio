using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Signals;
using Game.Shared;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MessagePipe;
using Game.Shared.Netcode.Client;
using Game.Shared.Netcode.Survivor;
using Game.Shared.Services.Network;
using Game.Shared.Services.Network.Cache;
using Game.Shared.Services.Network.Connectivity;
using Game.Shared.Services.Network.Policies;
using Game.Shared.Services.Network.Queue;
using VContainer;
using VContainer.Unity;
using AudioSaveService = Game.Shared.SaveData.AudioSaveService;
using IAudioSaveService = Game.Shared.SaveData.IAudioSaveService;
using AuthApiService = Game.Shared.Services.AuthApiService;
using AuthSessionService = Game.Shared.Services.AuthSessionService;
using SurvivorScoreApiService = Game.Shared.Services.SurvivorScoreApiService;
using UnityApiClient = Game.Shared.Services.UnityApiClient;
using Game.Shared.Chat.Client;
using Game.Shared.Realtime.Client;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivor用のVContainer LifetimeScope
    /// MVP.Coreのシーンサービスと、Survivor固有のサービス/モデルを登録
    /// </summary>
    public class SurvivorLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // MessagePipe（VContainer統合）
            var messagePipeOptions = builder.RegisterMessagePipe();
            RegisterMessageBrokers(builder, messagePipeOptions);
            RegisterNetworkSignalBrokers(builder, messagePipeOptions);

            // Core Services
            builder.Register<AddressableAssetService>(Lifetime.Singleton).As<IAddressableAssetService>();
            builder.Register<GameSceneService>(Lifetime.Singleton).As<IGameSceneService>();
            builder.Register<MasterDataService>(Lifetime.Singleton).As<IMasterDataService>();
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>();
            builder.Register<InputService>(Lifetime.Singleton).As<IInputService>();
            // memo: 必要な時に入れる
            // builder.Register<ScopedServiceContainer>(Lifetime.Singleton).As<IScopedServiceContainer>();
            // builder.RegisterEntryPoint<TickableService>().As<ITickableService>();

            // Save Data Storage（共通のセーブデータI/O、暗号化デコレーター付き）
            builder.Register<SaveDataStorage>(Lifetime.Singleton);
            builder.Register<ISaveDataStorage>(
                resolver => new EncryptedSaveDataStorage(resolver.Resolve<SaveDataStorage>()),
                Lifetime.Singleton);

            // Persistent Object Provider（ゲーム起動時に生成される永続オブジェクトを保持）
            builder.Register<PersistentObjectProvider>(Lifetime.Singleton).As<IPersistentObjectProvider>();

            // Game Root Controller（PersistentObjectProviderから取得）
            // Transient: StartupAsync完了後に有効になるため、毎回取得する
            builder.Register<IGameRootController>(
                resolver => resolver.Resolve<IPersistentObjectProvider>().Get<IGameRootController>(),
                Lifetime.Transient);

            // Save Services
            builder.Register<SurvivorSaveService>(Lifetime.Singleton).As<ISurvivorSaveService>();
            builder.Register<AudioSaveService>(Lifetime.Singleton).As<IAudioSaveService>();

            // Lock-On Service（ロックオン機能）
            builder.Register<LockOnService>(Lifetime.Singleton).As<ILockOnService>();

            // ========================================
            // Network Services（登録順序重要）
            // ========================================

            // 1. CircuitBreakerPolicy（他のサービスより先に登録）
            builder.RegisterInstance(CircuitBreakerPolicy.Default);

            // 2. ConnectivityChecker（パラメータなしコンストラクタを使用、デフォルト5秒間隔）
            builder.Register<IConnectivityChecker>(_ => new ConnectivityChecker(), Lifetime.Singleton);

            // 3. NetworkService（IConnectivityChecker + CircuitBreakerPolicyに依存）
            builder.Register<NetworkService>(Lifetime.Singleton).As<INetworkService>();

            // 4. ResponseCache
            builder.Register<IResponseCache>(_ => new MemoryResponseCache(), Lifetime.Singleton);

            // 5. RequestSigningService（鍵はログイン後にサーバーから配布される）
            builder.Register<RequestSigningService>(Lifetime.Singleton).As<IRequestSigningService>();

            // 6. API Client（INetworkService + IResponseCache + IRequestSigningServiceに依存）
            builder.Register<UnityApiClient>(Lifetime.Singleton).As<IApiClient>();

            // ========================================
            // API Services（IApiClientのみに依存）
            // ========================================
            builder.Register<AuthSessionService>(Lifetime.Singleton).As<IAuthSessionService>();
            builder.Register<AuthApiService>(Lifetime.Singleton).As<IAuthApiService>();
            builder.Register<SurvivorScoreApiService>(Lifetime.Singleton).As<ISurvivorScoreApiService>();

            // Request Queue & Notifications
            builder.Register<MemoryRequestQueue>(Lifetime.Singleton).As<IRequestQueue>();
            builder.Register<QueueNotificationService>(Lifetime.Singleton).As<IQueueNotificationService>();

            // ========================================
            // NGO Client Services
            // ========================================
            builder.Register<NetworkSurvivorStageClient>(Lifetime.Singleton);

            // ========================================
            // MagicOnion Services（gRPC Unary + StreamingHub）
            // ========================================
            builder.Register<GrpcChannelProvider>(Lifetime.Singleton).As<IGrpcChannelProvider>();
            builder.Register<AuthClientFilter>(Lifetime.Singleton);
            builder.Register<MatchmakingClient>(Lifetime.Singleton).As<IMatchmakingClient>();
            builder.Register<LobbyClient>(Lifetime.Singleton).As<ILobbyClient>();

            // ========================================
            // Chat Services（REST + SignalR）
            // ========================================
            builder.Register<ChatClient>(Lifetime.Singleton).As<IChatClient>();

            // Game Runner (Entry Point)
            builder.Register<SurvivorGameRunner>(Lifetime.Singleton).As<ISurvivorGameRunner>();

            // Note: シーン（Presenter）はGameSceneServiceがnew() + Inject()で生成するため登録不要
            // Note: SurvivorStageModel, SurvivorStageWaveManager は SurvivorStageScene が直接所有
        }

        private static void RegisterNetworkSignalBrokers(IContainerBuilder builder, MessagePipeOptions options)
        {
            // Session
            builder.RegisterMessageBroker<SurvivorNetworkSignals.AllPlayersReady>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.GameStarted>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.GameEnded>(options);

            // Connection
            builder.RegisterMessageBroker<SurvivorNetworkSignals.PlayerConnected>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.PlayerDisconnected>(options);

            // Player
            builder.RegisterMessageBroker<SurvivorNetworkSignals.PlayerDamaged>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.PlayerDied>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.ItemCollected>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.PlayerLeveledUp>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.WeaponChanged>(options);

            // Enemy
            builder.RegisterMessageBroker<SurvivorNetworkSignals.EnemyKilled>(options);

            // Wave
            builder.RegisterMessageBroker<SurvivorNetworkSignals.WaveStarted>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.WaveCleared>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.AllWavesCleared>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.TimeUp>(options);

            // Pause
            builder.RegisterMessageBroker<SurvivorNetworkSignals.GamePaused>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.GameResumed>(options);

            // Batch sync
            builder.RegisterMessageBroker<SurvivorNetworkSignals.EnemyBatchUpdated>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.ItemSpawned>(options);
            builder.RegisterMessageBroker<SurvivorNetworkSignals.ItemDespawned>(options);
        }

        private static void RegisterMessageBrokers(IContainerBuilder builder, MessagePipeOptions options)
        {
            // Player signals
            builder.RegisterMessageBroker<SurvivorSignals.Player.Spawned>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.Died>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.DamageReceived>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.LevelUp>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.ExperienceGained>(options);

            // Enemy signals
            builder.RegisterMessageBroker<SurvivorSignals.Enemy.Spawned>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Enemy.Killed>(options);

            // Wave signals
            builder.RegisterMessageBroker<SurvivorSignals.Wave.Started>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Wave.Completed>(options);

            // Game signals
            builder.RegisterMessageBroker<SurvivorSignals.Game.Paused>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Game.Resumed>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Game.Victory>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Game.GameOver>(options);
        }
    }
}
