using System;
using UnityEngine;

namespace Game.Core
{
    public partial class GameManager
    {
        public static readonly GameManager Instance = new();

        private GameConfig _gameConfig;

        private GameManager()
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EntryPoint()
        {
            Instance.GameStart();
        }

        public void GameStart()
        {
            LoadConfig();
            AppQuitIfJailbreak();
            GameServiceManager.Instance.StartUp();
            GameServiceManager.Instance.GetService<HelloWorldGameService>().HelloWorld();
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
        /// アプリ脱獄チェックして、黒なら強制終了
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
        }
    }
}