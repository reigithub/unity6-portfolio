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

            // 各種サービス取得・初期化
            var dbService = GameServiceManager.Get<ScriptableDatabaseService>();
            await dbService.LoadAsync();

            var audioService = GameServiceManager.Get<AudioService>();
            await audioService.LoadAsync();

            GameServiceManager.Add<MessagePipeService>();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();

            // 共通オブジェクト読み込み
            await HorrorGameRootController.LoadAssetAsync();

            // オプション設定: ロード → 共有登録 → 起動時の静的適用
            var saveDataStorage = new SaveDataStorage();
            var optionSaveService = new HorrorOptionSaveService(saveDataStorage);
            await optionSaveService.LoadAsync();
            GameServiceManager.Register<HorrorOptionSaveService>(optionSaveService);
            HorrorOptionHelper.ApplySaveData(optionSaveService.Data);

            // キーリバインドのオーバーライドを起動時に適用
            var inputSystemService = GameServiceManager.Get<InputSystemService>();
            inputSystemService.LoadBindingOverrides(optionSaveService.Data.InputBindingOverridesJson);

            // オーディオ設定
            audioService.SetVolume(
                optionSaveService.Data.MasterVolume,
                optionSaveService.Data.BgmVolume,
                optionSaveService.Data.VoiceVolume,
                optionSaveService.Data.SeVolume);

            // インベントリ: ロード（マスター整合込み）→ 共有登録（saveDataStorage を共有）
            var inventorySaveService = new HorrorInventorySaveService(saveDataStorage, dbService);
            await inventorySaveService.LoadAsync();
            GameServiceManager.Register<HorrorInventorySaveService>(inventorySaveService);

            // 5. 初期シーン遷移
            await gameSceneService.TransitionAsync<HorrorTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            await HorrorGameRootController.UnloadAsync();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();
            await gameSceneService.TerminateAllAsync();
            var audioService = GameServiceManager.Get<AudioService>();
            audioService.Unload();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}
