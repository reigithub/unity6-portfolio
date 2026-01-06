using System;
using System.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.Constants;
using Game.Core.Extensions;
using UnityEngine;
using Game.Core.Services;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public sealed partial class GameManager
    {
        private static readonly Lazy<GameManager> InstanceLazy = new(() => new GameManager());
        public static GameManager Instance => InstanceLazy.Value;

        // public static readonly GameManager Instance = new();

        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<GameSceneService> _sceneService;
        private GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MasterDataService> _masterDataService;
        private MasterDataService MasterDataService => _masterDataService.Reference;

        private GameConfig _gameConfig;

        private GameManager()
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EntryPoint()
        {
            Instance.GameStart();
        }

        private void GameStart()
        {
#if UNITY_EDITOR
            var scene = SceneManager.GetActiveScene();
            if (scene.name != GameSceneConstants.GameRootScene)
                return;
#endif

            LoadConfig();
            AppQuitIfJailbreak();
            GameStartAsync().Forget();
        }

        // ゲーム環境設定を読み込む機能（開発とか本番とか）
        private void LoadConfig()
        {
            _gameConfig ??= GameConfigManager.Load();
            if (_gameConfig is null)
            {
                throw new InvalidOperationException("ゲーム環境設定が読み込まれていない為、スタート処理に失敗しました");
            }
        }

        /// <summary>
        /// アプリ脱獄チェックして、強制終了
        /// </summary>
        private void AppQuitIfJailbreak()
        {
            if (!Application.genuineCheckAvailable)
                return;

            if (!Application.genuine)
            {
                Application.Quit(-1);
            }
        }

        private async Task GameStartAsync()
        {
            // ゲーム起動時に初期化しておきたいサービスはここで
            GameServiceManager.Instance.StartUp();
            GameServiceManager.Instance.StartupService<MasterDataService>();
            GameServiceManager.Instance.StartupService<MessageBrokerService>();
            GameServiceManager.Instance.StartupService<AudioService>();
            GameServiceManager.Instance.StartupService<GameSceneService>();

            await GameCommonObjects.LoadAssetAsync();

            await MasterDataService.LoadMasterDataAsync();

            await SceneService.TransitionAsync<GameTitleScene>();
        }

        public void GameReStart()
        {
            // _sceneService.Reference.TransitionAsync<GameTitleScene>().Forget();
        }

        public void GameQuit()
        {
            GameServiceManager.Instance.Shutdown();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}