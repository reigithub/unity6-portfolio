using Cysharp.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.Services;

namespace Game.Core.Bootstrap
{
    public interface IGameLauncher
    {
        UniTask StartupAsync();
        void Shutdown();
    }

    /// <summary>
    /// GameServiceManagerを使用した従来の起動方式
    /// </summary>
    public class GameLauncher : IGameLauncher
    {
        public async UniTask StartupAsync()
        {
            // 1. サービスマネージャー初期化
            GameServiceManager.Instance.StartUp();

            // 2. 各種サービス取得・初期化
            var masterDataService = GameServiceManager.Instance.GetService<MasterDataService>();
            var messageBrokerService = GameServiceManager.Instance.GetService<MessageBrokerService>();
            var audioService = GameServiceManager.Instance.GetService<AudioService>();
            var gameSceneService = GameServiceManager.Instance.GetService<GameSceneService>();

            messageBrokerService.Startup();
            audioService.Startup();

            // 3. 共通オブジェクト読み込み
            await GameRootController.LoadAssetAsync();

            // 4. マスターデータ読み込み
            await masterDataService.LoadMasterDataAsync();

            // 5. 初期シーン遷移
            await gameSceneService.TransitionAsync<GameTitleScene>();
        }

        public void Shutdown()
        {
            GameServiceManager.Instance.Shutdown();
        }
    }
}