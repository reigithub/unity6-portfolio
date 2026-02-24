using System;
using System.IO;
using UnityEngine;

namespace Game.Shared
{
    public enum GameEnvironment
    {
        None = -1,      // 環境切替エラーハンドリング用
        Local = 0,      // ローカル環境
        Develop = 1,    // デバッグ・開発ビルド環境
        Staging = 1000, // QA・ステージングビルド環境
        Review = 2000,  // ストア審査・レビュービルド環境
        Release = 3000, // 本番・リリースビルド環境
    }

    public enum DependencyResolverMode
    {
        ServiceLocator = 0, // GameServiceManager
        DiContainer = 1,    // VContainer
    }

    /// <summary>
    /// Addressables 環境設定
    /// </summary>
    [Serializable]
    public class AddressablesEnvironmentConfig
    {
        [SerializeField] private string _profileName = "Default";
        [SerializeField] private bool _useRemoteLoadPath;
        [SerializeField] private bool _buildRemoteCatalog;

        public string ProfileName => _profileName;
        public bool UseRemoteLoadPath => _useRemoteLoadPath;
        public bool BuildRemoteCatalog => _buildRemoteCatalog;
    }

    [Serializable]
    public class GameEnvironmentConfig
    {
        [SerializeField] private GameEnvironment _environment;
        [SerializeField] private DependencyResolverMode _dependencyResolverMode;
        [SerializeField] private string _apiBaseUrl;
        [SerializeField] private string _grpcBaseUrl;
        [SerializeField] private string _webSocketUrl;
        [SerializeField] private bool _enableDebugLog;
        [SerializeField] private bool _enableAnalytics;
        [SerializeField] private bool _useLocalMasterData;
        [SerializeField] private AddressablesEnvironmentConfig _addressablesConfig;

        public GameEnvironment Environment => _environment;
        public DependencyResolverMode DependencyResolverMode => _dependencyResolverMode;
        public string ApiBaseUrl => _apiBaseUrl;
        public string GrpcBaseUrl => _grpcBaseUrl;
        public string WebSocketUrl => _webSocketUrl;
        public bool EnableDebugLog => _enableDebugLog;
        public bool EnableAnalytics => _enableAnalytics;
        public bool UseLocalMasterData => _useLocalMasterData;
        public AddressablesEnvironmentConfig AddressablesConfig => _addressablesConfig;
    }

    public static class GameEnvironmentHelper
    {
        private static GameEnvironment? _overrideEnvironment;
        private static string _instanceSubPath;

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
                return GameEnvironmentSettings.Instance?.Environment ?? GameEnvironment.None;
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
        /// 環境ごとのデータパス
        /// RELEASE: Application.persistentDataPath（変換なし）
        /// それ以外: Application.persistentDataPath/{Environment}（環境ごとにフォルダ分離）
        /// </summary>
        public static string PersistentDataPath
        {
            get
            {
#if RELEASE
                return Application.persistentDataPath;
#else
                var basePath = Application.persistentDataPath;
                var envFolder = Current.ToString();
                var path = Path.Combine(basePath, envFolder);

                // MPPMクローン等でインスタンスサブパスが設定されている場合は挿入
                if (!string.IsNullOrEmpty(_instanceSubPath))
                {
                    path = Path.Combine(path, _instanceSubPath);
                }

                // ディレクトリが存在しない場合は作成
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
#endif
            }
        }

        /// <summary>
        /// インスタンス固有のサブパスを設定（MPPMクローン分離用）
        /// PersistentDataPath が {base}/{env}/{subPath}/ となる
        /// </summary>
        public static void SetInstanceSubPath(string subPath)
        {
            _instanceSubPath = subPath;
            Debug.Log($"[GameEnvironmentHelper] Instance sub-path set: {subPath}");
        }

        public static bool Validate()
        {
            CheckCommandLineArgs();

            bool valid = false;
            switch (Current)
            {
                case GameEnvironment.Local:
#if UNITY_EDITOR
                    valid = true;
#endif
                    break;
                case GameEnvironment.Develop:
#if DEVELOP || UNITY_EDITOR
                    valid = true;
#endif
                    break;
                case GameEnvironment.Staging:
#if STAGING || UNITY_EDITOR
                    valid = true;
#endif
                    break;
                case GameEnvironment.Review:
#if REVIEW || UNITY_EDITOR
                    valid = true;
#endif
                    break;
                case GameEnvironment.Release:
#if RELEASE || UNITY_EDITOR
                    valid = true;
#endif
                    break;
            }

            return valid;
        }

        /// <summary>
        /// 起動引数から環境をオーバーライド（開発用）
        /// </summary>
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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
