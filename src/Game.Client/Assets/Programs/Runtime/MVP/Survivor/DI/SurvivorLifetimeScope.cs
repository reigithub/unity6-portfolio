using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Server;
using Game.Shared;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MessagePipe;
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
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Realtime.Client;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using UnityEngine;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivor用のVContainer LifetimeScope
    /// MVP.Coreのシーンサービスと、Survivor固有のサービス/モデルを登録
    /// UnityPlaymodeHelper.IsServer() でサーバー/クライアントのDI登録をランタイム分岐
    /// </summary>
    public class SurvivorLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // MessagePipe（VContainer統合）
            var messagePipeOptions = builder.RegisterMessagePipe();
            RegisterSignalBrokers(builder, messagePipeOptions);

            // ========================================
            // 共通サービス（サーバー・クライアント両方で必要）
            // ========================================
            builder.Register<AddressableAssetService>(Lifetime.Singleton).As<IAddressableAssetService>();
            builder.Register<GameSceneService>(Lifetime.Singleton).As<IGameSceneService>();
            builder.Register<MasterDataService>(Lifetime.Singleton).As<IMasterDataService>();
            builder.Register<FusionRunnerService>(Lifetime.Singleton).As<IFusionRunnerService>();

            if (UnityPlaymodeHelper.IsServer())
            {
                RegisterServerServices(builder);
            }
            else
            {
                RegisterClientServices(builder);
            }
        }

        /// <summary>
        /// サーバー用サービス登録（Null/Server実装）
        /// </summary>
        private static void RegisterServerServices(IContainerBuilder builder)
        {
            // Null実装: サーバーでは不要だがDI注入先が要求するサービス
            builder.Register<NullAudioService>(Lifetime.Singleton).As<IAudioService>();
            builder.Register<NullInputService>(Lifetime.Singleton).As<IInputService>();
            builder.Register<NullLockOnService>(Lifetime.Singleton).As<ILockOnService>();
            builder.Register<NullPersistentObjectProvider>(Lifetime.Singleton).As<IPersistentObjectProvider>();

            // GameRootController: NullPersistentObjectProviderからnullを返す（全呼び出し元でnullチェック済み）
            builder.Register<IGameRootController>(
                resolver => resolver.Resolve<IPersistentObjectProvider>().Get<IGameRootController>(),
                Lifetime.Transient);

            // Server実装: セッション情報を供給可能なサーバー用セーブサービス
            builder.Register<SurvivorServerSaveService>(Lifetime.Singleton).As<ISurvivorSaveService>();

            // Fusion Server（サーバーモードでも Fusion 経由で接続）
            builder.Register<SurvivorFusionStageConnector>(Lifetime.Singleton).As<ISurvivorNetworkStageConnector>();

            // Local Server Orchestrator（サーバーでは不要）
            builder.Register<NullLocalServerOrchestrator>(Lifetime.Singleton).As<ILocalServerOrchestrator>();

            // Server Game Loop: AllPlayersReady → SurvivorNetworkStageScene 遷移
            builder.RegisterEntryPoint<SurvivorServerGameLoop>();
        }

        /// <summary>
        /// クライアント用サービス登録（既存の実装）
        /// </summary>
        private static void RegisterClientServices(IContainerBuilder builder)
        {
            // Core Services
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

            // ========================================
            // Fusion Client（クライアント接続用）
            // ========================================
            builder.Register<SurvivorFusionStageConnector>(Lifetime.Singleton).As<ISurvivorNetworkStageConnector>();

#if !UNITY_SERVER
            // ========================================
            // Local Server Orchestrator（UseLocalServerOrchestrator 有効時のみ実体を生成）
            // ========================================
            if (GameEnvironmentHelper.CurrentConfig?.UseLocalServerOrchestrator == true)
            {
                builder.Register<LocalServerOrchestrator>(Lifetime.Singleton)
                    .As<ILocalServerOrchestrator>();
            }
            else
            {
                builder.Register<NullLocalServerOrchestrator>(Lifetime.Singleton)
                    .As<ILocalServerOrchestrator>();
            }
#endif

            // Game Runner (Entry Point)
            builder.Register<SurvivorGameRunner>(Lifetime.Singleton).As<ISurvivorGameRunner>();
        }

        private static void RegisterSignalBrokers(IContainerBuilder builder, MessagePipeOptions options)
        {
            // Player
            builder.RegisterMessageBroker<SurvivorSignals.Player.Spawned>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.DamageReceived>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.Died>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.ItemCollected>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.LeveledUp>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Player.WeaponChanged>(options);

            // Enemy
            builder.RegisterMessageBroker<SurvivorSignals.Enemy.Killed>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Enemy.BatchUpdated>(options);

            // Wave
            builder.RegisterMessageBroker<SurvivorSignals.Wave.Started>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Wave.Completed>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Wave.AllCleared>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Wave.TimeUp>(options);

            // Game
            builder.RegisterMessageBroker<SurvivorSignals.Game.Ended>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Game.Paused>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Game.Resumed>(options);

            // Session
            builder.RegisterMessageBroker<SurvivorSignals.Session.AllPlayersReady>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Session.GameStarted>(options);

            // Connection
            builder.RegisterMessageBroker<SurvivorSignals.Connection.PlayerConnected>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Connection.PlayerDisconnected>(options);

            // Item
            builder.RegisterMessageBroker<SurvivorSignals.Item.Spawned>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Item.Despawned>(options);

            // Server
            builder.RegisterMessageBroker<SurvivorSignals.Weapon.HitReported>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Weapon.ApplyRequested>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Item.CollectReported>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Session.AllClientsSceneReady>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Session.ClientFieldSceneLoaded>(options);
            builder.RegisterMessageBroker<SurvivorSignals.Session.AllPlayersDisconnected>(options);
        }
    }
}
