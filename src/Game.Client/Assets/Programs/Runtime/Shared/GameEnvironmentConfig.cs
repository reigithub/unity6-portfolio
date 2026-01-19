using System;
using UnityEngine;

namespace Game.Shared
{
    public enum GameEnvironment
    {
        Local = 0,      // ローカル環境
        Develop = 1,    // 開発ビルド環境
        Staging = 1000, // ステージングビルド環境
        Review = 2000,  // レビュービルド環境
        Release = 3000, // 本番リリースビルド環境
    }

    public enum DependencyResolverMode
    {
        ServiceLocator = 0, // GameServiceManager
        DiContainer = 1,    // VContainer
    }

    [Serializable]
    public class GameEnvironmentConfig
    {
        [SerializeField] private GameEnvironment _environment;
        [SerializeField] private DependencyResolverMode _dependencyResolverMode;
        [SerializeField] private string _apiBaseUrl;
        [SerializeField] private string _webSocketUrl;
        [SerializeField] private bool _enableDebugLog;
        [SerializeField] private bool _enableAnalytics;
        [SerializeField] private bool _useLocalMasterData;

        public GameEnvironment Environment => _environment;
        public DependencyResolverMode DependencyResolverMode => _dependencyResolverMode;
        public string ApiBaseUrl => _apiBaseUrl;
        public string WebSocketUrl => _webSocketUrl;
        public bool EnableDebugLog => _enableDebugLog;
        public bool EnableAnalytics => _enableAnalytics;
        public bool UseLocalMasterData => _useLocalMasterData;
    }

    public static class GameEnvironmentHelper
    {
        private static GameEnvironment? _overrideEnvironment;

        /// <summary>
        /// 現在の環境を取得（優先順位: Define > Override > Settings）
        /// </summary>
        public static GameEnvironment Current
        {
            get
            {
                // 1. Scripting Define Symbols（最優先）
#if RELEASE
                  return GameEnvironment.Release;
#elif STAGING
                  return GameEnvironment.Staging;
#elif DEVELOP
                  return GameEnvironment.Develop;
#else
                // 2. 実行時オーバーライド
                if (_overrideEnvironment.HasValue)
                {
                    return _overrideEnvironment.Value;
                }

                // 3. ScriptableObject設定
                return GameEnvironmentSettings.Instance?.Environment ?? GameEnvironment.Develop;
#endif
            }
        }

        /// <summary>
        /// 現在の環境設定を取得
        /// </summary>
        public static GameEnvironmentConfig CurrentConfig => GameEnvironmentSettings.Instance?.CurrentConfig;

        /// <summary>
        /// 本番環境かどうか
        /// </summary>
        public static bool IsRelease => Current == GameEnvironment.Release;

        /// <summary>
        /// 開発環境かどうか
        /// </summary>
        public static bool IsDevelop => Current == GameEnvironment.Develop;

        /// <summary>
        /// デバッグログが有効かどうか
        /// </summary>
        public static bool IsDebugLogEnabled =>
#if RELEASE
              false;  // 本番では常に無効
#else
            CurrentConfig?.EnableDebugLog ?? true;
#endif

        /// <summary>
        /// 起動引数から環境をオーバーライド（開発用）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CheckCommandLineArgs()
        {
#if !RELEASE
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--environment" && i + 1 < args.Length)
                {
                    if (System.Enum.TryParse<GameEnvironment>(args[i + 1], true, out var env))
                    {
                        _overrideEnvironment = env;
                        Debug.Log($"[EnvironmentHelper] Override environment: {env}");
                    }
                }
            }

            // 環境変数からも取得可能
            var envVar = System.Environment.GetEnvironmentVariable("GAME_ENVIRONMENT");
            if (!string.IsNullOrEmpty(envVar) &&
                System.Enum.TryParse<GameEnvironment>(envVar, true, out var envFromVar))
            {
                _overrideEnvironment = envFromVar;
                Debug.Log($"[EnvironmentHelper] Override from env var: {envFromVar}");
            }
#endif
        }
    }
}