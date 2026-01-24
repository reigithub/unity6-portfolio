using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.ScoreTimeAttack.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.SaveData;

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
            GameServiceManager.Instance.StartUp();

            // 2. 各種サービス取得・初期化
            var masterDataService = GameServiceManager.Get<MasterDataService>();
            GameServiceManager.Add<MessagePipeService>();
            GameServiceManager.Add<AudioService>();
            var audioService = GameServiceManager.Get<AudioService>();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();

            // 3. 共通オブジェクト読み込み
            await GameResidentsManager.LoadAssetAsync();

            // 4. マスターデータ読み込み
            await masterDataService.LoadMasterDataAsync();

            // 5. オーディオ設定読み込み
            var saveDataStorage = new SaveDataStorage();
            var audioSaveService = new AudioSaveService(saveDataStorage, audioService);
            await audioSaveService.LoadAsync();

            // 6. 初期シーン遷移
            await gameSceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            var audioService = GameServiceManager.Get<AudioService>();
            audioService.StopBgmAsync().Forget();
            await audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.GameQuit);

            await GameResidentsManager.UnloadAsync();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();
            await gameSceneService.TerminateAllAsync();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}