using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Scenes;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;

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
            var dbService = new ScriptableDatabaseService();
            await dbService.LoadAsync();
            GameServiceManager.Register<IScriptableDatabaseService, ScriptableDatabaseService>(dbService);

            var audioService = GameServiceManager.Get<AudioService>();
            await audioService.LoadAsync();

            GameServiceManager.Add<MessagePipeService>();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();

            // 共通オブジェクト読み込み
            await HorrorGameRootController.LoadAssetAsync();

            // アイコン一括ロード
            var iconService = new HorrorIconService();
            await iconService.LoadAsync();
            GameServiceManager.Register<IHorrorIconService>(iconService);

            // セーブデータストレージ構築
            var keyProvider = new AppSharedKeyProvider();
            keyProvider.Prewarm();
            var saveDataStorage = new EncryptedSaveDataStorage(new SaveDataStorage(), keyProvider);

            // オプション設定: ロード → 共有登録 → 起動時の静的適用
            var optionSaveRepository = new HorrorOptionSaveRepository(saveDataStorage);
            await optionSaveRepository.LoadAsync();
            GameServiceManager.Register(optionSaveRepository);
            var optionService = new HorrorOptionService(optionSaveRepository);
            GameServiceManager.Register<IHorrorOptionService, HorrorOptionService>(optionService);
            HorrorOptionHelper.ApplySaveData(optionSaveRepository.Data);

            // キーリバインドのオーバーライドを起動時に適用
            var inputSystemService = GameServiceManager.Get<InputSystemService>();
            inputSystemService.LoadBindingOverrides(optionSaveRepository.Data.InputBindingOverridesJson);

            // オーディオ設定
            audioService.SetVolume(
                optionSaveRepository.Data.MasterVolume,
                optionSaveRepository.Data.BgmVolume,
                optionSaveRepository.Data.VoiceVolume,
                optionSaveRepository.Data.SeVolume);

            // セーブデータ: リポジトリをロード（マスター整合込み）→ 具象キーで共有登録
            var saveRepository = new HorrorSaveRepository(saveDataStorage, dbService);
            await saveRepository.LoadAsync();
            GameServiceManager.Register(saveRepository);

            // インベントリ → 装備（所持判定を注入）→ インタラクション → プレイヤーの順に生成し、I/F キーで共有登録
            var inventoryService = new HorrorInventoryService(saveRepository);
            GameServiceManager.Register<IHorrorInventoryService>(inventoryService);

            var equipmentService = new HorrorEquipmentService(saveRepository, inventoryService);
            GameServiceManager.Register<IHorrorEquipmentService>(equipmentService);

            var interactionService = new HorrorInteractionService(saveRepository);
            GameServiceManager.Register<IHorrorInteractionService>(interactionService);

            var playerService = new HorrorPlayerService(saveRepository);
            GameServiceManager.Register<IHorrorPlayerService>(playerService);

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
            var iconService = GameServiceManager.Resolve<IHorrorIconService>();
            iconService.Unload();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}
