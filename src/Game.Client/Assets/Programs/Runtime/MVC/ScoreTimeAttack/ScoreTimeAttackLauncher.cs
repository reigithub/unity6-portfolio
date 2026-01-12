using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Services;
using Game.MVC.ScoreTimeAttack.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;

namespace Game.MVC.ScoreTimeAttack
{
    /// <summary>
    /// GameServiceManagerを使用した従来の起動方式（MVC用）
    /// </summary>
    public class ScoreTimeAttackLauncher : IGameModeLauncher
    {
        public GameMode Mode => GameMode.MvcScoreTimeAttack;

        public async UniTask StartupAsync()
        {
            // 1. サービスマネージャー初期化
            GameServiceManager.Instance.StartUp();

            // 2. 各種サービス取得・初期化
            var masterDataService = GameServiceManager.Get<MasterDataService>();
            var messagePipeService = GameServiceManager.Get<MessagePipeService>();
            var audioService = GameServiceManager.Get<AudioService>();
            var gameSceneService = GameServiceManager.Get<GameSceneService>();

            // 3. 共通オブジェクト読み込み
            await GameRootController.LoadAssetAsync();

            // 4. マスターデータ読み込み
            await masterDataService.LoadMasterDataAsync();

            // 5. 初期シーン遷移
            await gameSceneService.TransitionAsync<GameTitleScene>();
        }

        public async UniTask ShutdownAsync()
        {
            await GameRootController.UnloadAsync();
            GameServiceManager.Instance.Shutdown();
            await UniTask.Yield();
        }
    }
}