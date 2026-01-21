using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Signals;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MessagePipe;
using VContainer;
using VContainer.Unity;

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

            // Core Services
            builder.Register<AddressableAssetService>(Lifetime.Singleton).As<IAddressableAssetService>();
            builder.Register<GameSceneService>(Lifetime.Singleton).As<IGameSceneService>();
            builder.Register<MasterDataService>(Lifetime.Singleton).As<IMasterDataService>();
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>();
            builder.Register<InputService>(Lifetime.Singleton).As<IInputService>();
            // memo: 必要な時に入れる
            // builder.Register<ScopedServiceContainer>(Lifetime.Singleton).As<IScopedServiceContainer>();
            // builder.RegisterEntryPoint<TickableService>().As<ITickableService>();

            // Save Data Storage（共通のセーブデータI/O）
            builder.Register<SaveDataStorage>(Lifetime.Singleton).As<ISaveDataStorage>();

            // Persistent Object Provider（ゲーム起動時に生成される永続オブジェクトを保持）
            builder.Register<PersistentObjectProvider>(Lifetime.Singleton).As<IPersistentObjectProvider>();

            // Game Root Controller（PersistentObjectProviderから取得）
            // Transient: StartupAsync完了後に有効になるため、毎回取得する
            builder.Register<IGameRootController>(
                resolver => resolver.Resolve<IPersistentObjectProvider>().Get<IGameRootController>(),
                Lifetime.Transient);

            // Save Service（Survivor固有のセーブ機能）
            builder.Register<SurvivorSaveService>(Lifetime.Singleton).As<ISurvivorSaveService>();

            // Lock-On Service（ロックオン機能）
            builder.Register<LockOnService>(Lifetime.Singleton).As<ILockOnService>();

            // Game Runner (Entry Point)
            builder.Register<SurvivorGameRunner>(Lifetime.Singleton).As<ISurvivorGameRunner>();

            // Note: シーン（Presenter）はGameSceneServiceがnew() + Inject()で生成するため登録不要
            // Note: SurvivorStageModel, SurvivorStageWaveManager は SurvivorStageScene が直接所有
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