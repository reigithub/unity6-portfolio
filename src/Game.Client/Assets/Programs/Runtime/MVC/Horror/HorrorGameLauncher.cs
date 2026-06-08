using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Scenes;
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

            // 4-2. オプション設定: ロード → 共有登録 → 起動時の静的適用
            var optionSaveService = new HorrorOptionSaveService(saveDataStorage);
            await optionSaveService.LoadAsync();
            GameServiceManager.Register<HorrorOptionSaveService>(optionSaveService);
            HorrorOptionHelper.ApplySaveData(optionSaveService.Data);

            // 5. 初期シーン遷移
            await gameSceneService.TransitionAsync<HorrorTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            await HorrorGameRootController.UnloadAsync();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();
            await gameSceneService.TerminateAllAsync();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}
