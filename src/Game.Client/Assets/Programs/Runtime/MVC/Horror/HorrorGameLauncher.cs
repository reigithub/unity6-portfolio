using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Horror.Events;
using Game.Horror.SaveData;
using Game.Horror.Scenes;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.Events;
using Game.Shared.SaveData;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;

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
            GameServiceManager.StartUp();

            // 各種サービス取得・初期化
            var assetService = new AddressableAssetService();
            GameServiceManager.Register<IAddressableAssetService, AddressableAssetService>(assetService);
            var localizationService = new LocalizationService();
            GameServiceManager.Register<ILocalizationService, LocalizationService>(localizationService);

            var dbService = new ScriptableDatabaseService(assetService);
            GameServiceManager.Register<IScriptableDatabaseService, ScriptableDatabaseService>(dbService);
            await dbService.LoadAsync();

            var audioService = new AudioService(assetService);
            GameServiceManager.Register<IAudioService, AudioService>(audioService);
            await audioService.LoadAsync();

            var messagePipeService = new MessagePipeService();
            {
                // 動作させるための暫定登録
                messagePipeService.AddMessageBroker<int, bool>();
                messagePipeService.AddMessageBroker<int, string>();
            }
            messagePipeService.AddMessageBroker<NoiseEvent>();
            messagePipeService.AddMessageBroker<HorrorDamageAppliedEvent>();
            messagePipeService.Build();
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(messagePipeService);

            // アイコン一括ロード
            var iconService = new HorrorIconService(assetService);
            GameServiceManager.Register<IHorrorIconService, HorrorIconService>(iconService);
            await iconService.LoadAsync();

            // セーブデータストレージ構築
            var keyProvider = new AppSharedKeyProvider();
            keyProvider.Prewarm();
            var saveDataStorage = new EncryptedSaveDataStorage(new SaveDataStorage(), keyProvider);

            // オプション設定: ロード → 共有登録 → 起動時の静的適用
            var optionSaveRepository = new HorrorOptionSaveRepository(saveDataStorage);
            GameServiceManager.Register<IHorrorOptionSaveRepository, HorrorOptionSaveRepository>(optionSaveRepository);
            await optionSaveRepository.LoadAsync();

            var optionService = new HorrorOptionService(optionSaveRepository);
            GameServiceManager.Register<IHorrorOptionService, HorrorOptionService>(optionService);
            HorrorOptionHelper.ApplySaveData(optionSaveRepository.Data);

            // キーリバインドのオーバーライドを起動時に適用
            var inputSystemService = new InputSystemService(localizationService);
            GameServiceManager.Register<IInputSystemService, InputSystemService>(inputSystemService);
            inputSystemService.LoadBindingOverrides(optionSaveRepository.Data.InputBindingOverridesJson);

            // オーディオ設定
            audioService.SetVolume(
                optionSaveRepository.Data.MasterVolume,
                optionSaveRepository.Data.BgmVolume,
                optionSaveRepository.Data.VoiceVolume,
                optionSaveRepository.Data.SeVolume);

            // セーブデータ: リポジトリをロード（マスター整合込み）→ 具象キーで共有登録
            var saveRepository = new HorrorSaveRepository(saveDataStorage, dbService);
            GameServiceManager.Register<IHorrorSaveRepository, HorrorSaveRepository>(saveRepository);

            // インベントリ → 装備（所持判定を注入）→ インタラクション → プレイヤーの順に生成し、I/F キーで共有登録
            var inventoryService = new HorrorInventoryService(saveRepository);
            GameServiceManager.Register<IHorrorInventoryService, HorrorInventoryService>(inventoryService);

            var equipmentService = new HorrorEquipmentService(saveRepository, inventoryService);
            GameServiceManager.Register<IHorrorEquipmentService, HorrorEquipmentService>(equipmentService);

            var interactionService = new HorrorInteractionService(saveRepository);
            GameServiceManager.Register<IHorrorInteractionService, HorrorInteractionService>(interactionService);

            var playerService = new HorrorPlayerService(saveRepository);
            GameServiceManager.Register<IHorrorPlayerService, HorrorPlayerService>(playerService);

            // 共通オブジェクト読み込み
            await HorrorGameRootController.LoadAssetAsync();

            // 5. 初期シーン遷移
            var gameSceneService = new GameSceneService(inputSystemService);
            GameServiceManager.Register<IGameSceneService, GameSceneService>(gameSceneService);
            await gameSceneService.TransitionAsync<HorrorTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            await HorrorGameRootController.UnloadAsync();
            var gameSceneService = GameServiceManager.Resolve<IGameSceneService>();
            await gameSceneService.TerminateAllAsync();
            var audioService = GameServiceManager.Resolve<IAudioService>();
            audioService.Unload();
            var iconService = GameServiceManager.Resolve<IHorrorIconService>();
            iconService.Unload();
            GameServiceManager.Shutdown();
            await UniTask.Yield();
        }
    }
}
