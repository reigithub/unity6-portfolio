using System;
using System.Threading.Tasks;
using Game.Core.Constants;
using Game.Core.Extensions;
using UnityEngine;
using Game.Core.Services;
using Sample;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public sealed partial class GameManager
    {
        private static readonly Lazy<GameManager> InstanceLazy = new(() => new GameManager());
        public static GameManager Instance => InstanceLazy.Value;

        // public static readonly GameManager Instance = new();

        private GameConfig _gameConfig;
        private GameServiceReference<AddressableAssetService> _assetService;
        private GameServiceReference<GameSceneService> _sceneService;

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
            GameServiceManager.Instance.StartUp();
            GameServiceManager.Instance.AddService<MessageBrokerService>();
            GameStartAsync().Forget();
        }

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
            await GameCommonObjects.LoadAssetAsync();
            await _sceneService.Reference.TransitionAsync<GameTitleScene>();
        }

        public void GameReStart()
        {
        }

        public void GameSuspend()
        {
        }

        public void GameResume()
        {
        }

        public void GameExit()
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