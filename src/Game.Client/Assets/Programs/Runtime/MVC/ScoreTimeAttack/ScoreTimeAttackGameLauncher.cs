using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.ScoreTimeAttack.Scenes;
using Game.ScoreTimeAttack.Services;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;

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

            var masterDataService = new MasterDataService(assetService);
            await masterDataService.LoadMasterDataAsync();
            GameServiceManager.Register<IMasterDataService, MasterDataService>(masterDataService);

            var audioService = new AudioService(assetService, masterDataService);
            await audioService.LoadAsync();
            GameServiceManager.Register<IAudioService, AudioService>(audioService);
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(new MessagePipeService());
            var inputSystemService = new InputSystemService();
            GameServiceManager.Register<IInputSystemService, InputSystemService>(inputSystemService);

            // 3. 共通オブジェクト読み込み
            await GameRootController.LoadAssetAsync();

            // 5. オーディオ設定読み込み
            var saveDataStorage = new SaveDataStorage();
            var audioSaveService = new AudioSaveService(saveDataStorage, audioService);
            await audioSaveService.LoadAsync();

            // 6. 初期シーン遷移
            var gameSceneService = new GameSceneService(inputSystemService);
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
