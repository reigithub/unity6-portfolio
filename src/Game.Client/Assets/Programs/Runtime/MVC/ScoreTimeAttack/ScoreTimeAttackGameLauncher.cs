using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.ScoreTimeAttack.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using UnityEngine;

namespace Game.ScoreTimeAttack
{
    /// <summary>
    /// GameServiceManagerを使用した従来の起動方式（MVC用）
    /// </summary>
    public class ScoreTimeAttackGameLauncher : IGameModeLauncher
    {
        public GameMode Mode => GameMode.MvcScoreTimeAttack;

        public async UniTask StartupAsync()
        {
            // 1. サービスマネージャー初期化
            GameServiceManager.StartUp();

            // 2. 各種サービス取得・初期化
            var assetService = new AddressableAssetService();
            GameServiceManager.Register<IAddressableAssetService, AddressableAssetService>(assetService);
            var localizationService = new LocalizationService();
            GameServiceManager.Register<ILocalizationService, LocalizationService>(localizationService);

            var messagePipeService = new MessagePipeService();
            messagePipeService.AddMessageBroker<int, int>();
            messagePipeService.AddMessageBroker<int, float>();
            messagePipeService.AddMessageBroker<int, bool>();
            messagePipeService.AddMessageBroker<int, string>();
            messagePipeService.AddMessageBroker<int, GameObject>();
            messagePipeService.AddMessageBroker<int, Vector2>();
            messagePipeService.Build();
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(messagePipeService);

            var masterDataService = new MasterDataService(assetService);
            await masterDataService.LoadMasterDataAsync();
            GameServiceManager.Register<IMasterDataService, MasterDataService>(masterDataService);

            var audioService = new AudioService(assetService, masterDataService);
            await audioService.LoadAsync();
            GameServiceManager.Register<IAudioService, AudioService>(audioService);
            var inputSystemService = new InputSystemService(localizationService);
            GameServiceManager.Register<IInputSystemService, InputSystemService>(inputSystemService);

            // 3. 共通オブジェクト読み込み
            await GameRootController.LoadAssetAsync();

            // 5. オーディオ設定読み込み
            var saveDataStorage = new SaveDataStorage();
            var audioSaveService = new AudioSaveService(saveDataStorage, audioService);
            await audioSaveService.LoadAsync();

            // 6. 初期シーン遷移
            var gameSceneService = new GameSceneService();
            GameServiceManager.Register<IGameSceneService, GameSceneService>(gameSceneService);
            await gameSceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            var audioService = GameServiceManager.Resolve<IAudioService>();
            audioService.StopBgmAsync().Forget();
            await audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.GameQuit);

            await GameRootController.UnloadAsync();
            var gameSceneService = GameServiceManager.Resolve<IGameSceneService>();
            await gameSceneService.TerminateAllAsync();
            GameServiceManager.Shutdown();
            await UniTask.Yield();
        }
    }
}
