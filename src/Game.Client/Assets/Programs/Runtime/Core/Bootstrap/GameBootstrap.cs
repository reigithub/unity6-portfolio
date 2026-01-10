using Cysharp.Threading.Tasks;
using Game.Core.Constants;
using UnityEngine;

namespace Game.Core.Bootstrap
{
    public static class GameBootstrap
    {
        private static IGameLauncher _launcher;
        private static bool _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            if (_isInitialized) return;
            _isInitialized = true;

#if UNITY_EDITOR
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != GameSceneConstants.GameRootScene)
                return;
#endif

            // アプリ脱獄チェック
            if (Application.genuineCheckAvailable && !Application.genuine)
            {
                Application.Quit(-1);
                return;
            }

            // 設定に基づいてランチャーを選択
            var settings = GameEnvironmentSettings.Instance.CurrentConfig;
            _launcher = settings.DiContainerMode switch
            {
                GameDiContainerMode.VContainer => new VContainerGameLauncher(),
                _ => new GameLauncher()
            };

            Debug.Log($"[GameBootstrap] Mode: {settings.DiContainerMode}");

            // 起動
            _launcher.StartupAsync().Forget();
        }

        public static void Shutdown()
        {
            _launcher?.Shutdown();
            _launcher = null;
            _isInitialized = false;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}