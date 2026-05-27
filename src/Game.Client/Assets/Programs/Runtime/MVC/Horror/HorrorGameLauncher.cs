using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Horror.Scenes;
using Game.Library.Shared.Enums;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.SaveData;

namespace Game.Horror
{
    /// <summary>
    /// GameServiceManagerを使用した起動方式（MVC.Horror用）
    /// </summary>
    public class HorrorGameLauncher : IGameModeLauncher
    {
        public GameMode Mode => GameMode.MvcHorror;

        public async UniTask StartupAsync()
        {
            // 1. サービスマネージャー初期化
            GameServiceManager.Instance.StartUp();

            // 2. 各種サービス取得・初期化
            GameServiceManager.Add<MessagePipeService>();
            var audioService = GameServiceManager.Get<AudioService>();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();

            // 3. 共通オブジェクト読み込み
            await HorrorGameRootController.LoadAssetAsync();

            // 4. オーディオ設定読み込み
            var saveDataStorage = new SaveDataStorage();
            var audioSaveService = new AudioSaveService(saveDataStorage, audioService);
            await audioSaveService.LoadAsync();

            // 5. 初期シーン遷移
            await gameSceneService.TransitionAsync<HorrorTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            // var audioService = GameServiceManager.Get<AudioService>();
            // audioService.StopBgmAsync().Forget();
            // await audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.GameQuit);

            await HorrorGameRootController.UnloadAsync();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();
            await gameSceneService.TerminateAllAsync();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}
