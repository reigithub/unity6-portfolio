using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor.Build
{
    /// <summary>
    /// CI/CD用ビルドスクリプト
    /// GitHub Actions から -executeMethod で呼び出される
    /// </summary>
    public static class BuildScript
    {
        /// <summary>
        /// ビルドに含めるシーン（EditorBuildSettingsから取得しない場合のフォールバック）
        /// </summary>
        private static readonly string[] DefaultScenes = new[]
        {
            "Assets/ProjectAssets/GameRootScene.unity"
        };

        private static string ProjectPath => Path.GetDirectoryName(Application.dataPath);
        private static string BuildsPath => Path.Combine(ProjectPath, "Builds");

        /// <summary>
        /// コマンドライン引数からビルドタイムスタンプを取得
        /// -buildTimestamp YYYYMMDD_HHMMSS 形式で指定
        /// </summary>
        private static string BuildTimestamp
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-buildTimestamp")
                    {
                        return args[i + 1];
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// タイムスタンプ付きのビルドパスを生成
        /// CI ビルド時: Builds/CiBuilds/Platform/YYYYMMDD_HHMMSS/
        /// 手動ビルド時: Builds/Platform/
        /// </summary>
        private static string GetBuildPath(string platform, string fileName = null)
        {
            var timestamp = BuildTimestamp;
            string basePath;

            if (!string.IsNullOrEmpty(timestamp))
            {
                // CI ビルド: Builds/CiBuilds/Platform/Timestamp/
                basePath = Path.Combine(BuildsPath, "CiBuilds", platform, timestamp);
            }
            else
            {
                // 手動ビルド: Builds/Platform/
                basePath = Path.Combine(BuildsPath, platform);
            }

            if (string.IsNullOrEmpty(fileName))
            {
                return basePath;
            }

            return Path.Combine(basePath, fileName);
        }

        #region Public Build Methods

        /// <summary>
        /// WebGL ビルドを実行
        /// </summary>
        [MenuItem("Build/Build WebGL")]
        public static void BuildWebGL()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("WebGL");

            // WebGL用の設定を適用
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 512;

            BuildPlayer(scenes, buildPath, BuildTarget.WebGL, BuildOptions.None);
        }

        /// <summary>
        /// Windows (64-bit) ビルドを実行
        /// </summary>
        [MenuItem("Build/Build Windows (64-bit)")]
        public static void BuildWindows()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Windows", $"{Application.productName}.exe");

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        }

        /// <summary>
        /// Windows (64-bit) 開発ビルドを実行
        /// </summary>
        [MenuItem("Build/Build Windows (64-bit) Development")]
        public static void BuildWindowsDevelopment()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Windows-Dev", $"{Application.productName}.exe");

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneWindows64,
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// Linux (64-bit) ビルドを実行
        /// Docker コンテナ上の Runner で使用
        /// </summary>
        [MenuItem("Build/Build Linux (64-bit)")]
        public static void BuildLinux()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Linux", Application.productName);

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneLinux64, BuildOptions.None);
        }

        /// <summary>
        /// Linux (64-bit) 開発ビルドを実行
        /// </summary>
        [MenuItem("Build/Build Linux (64-bit) Development")]
        public static void BuildLinuxDevelopment()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Linux-Dev", Application.productName);

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneLinux64,
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// macOS ビルドを実行
        /// </summary>
        [MenuItem("Build/Build macOS")]
        public static void BuildMacOS()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("macOS", $"{Application.productName}.app");

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneOSX, BuildOptions.None);
        }

        /// <summary>
        /// macOS 開発ビルドを実行
        /// </summary>
        [MenuItem("Build/Build macOS Development")]
        public static void BuildMacOSDevelopment()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("macOS-Dev", $"{Application.productName}.app");

            BuildPlayer(scenes, buildPath, BuildTarget.StandaloneOSX,
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// Android ビルドを実行
        /// </summary>
        [MenuItem("Build/Build Android")]
        public static void BuildAndroid()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Android", $"{Application.productName}.apk");

            // Android用の設定
            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.None);
        }

        /// <summary>
        /// Android App Bundle (AAB) ビルドを実行
        /// Google Play Store 用
        /// </summary>
        [MenuItem("Build/Build Android (AAB)")]
        public static void BuildAndroidAAB()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Android", $"{Application.productName}.aab");

            // Android App Bundle を有効化
            EditorUserBuildSettings.buildAppBundle = true;

            BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.None);
        }

        /// <summary>
        /// Android 開発ビルドを実行
        /// </summary>
        [MenuItem("Build/Build Android Development")]
        public static void BuildAndroidDevelopment()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("Android-Dev", $"{Application.productName}.apk");

            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayer(scenes, buildPath, BuildTarget.Android,
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// iOS ビルドを実行
        /// Xcode プロジェクトを出力
        /// </summary>
        [MenuItem("Build/Build iOS")]
        public static void BuildIOS()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("iOS");

            BuildPlayer(scenes, buildPath, BuildTarget.iOS, BuildOptions.None);
        }

        /// <summary>
        /// iOS 開発ビルドを実行
        /// </summary>
        [MenuItem("Build/Build iOS Development")]
        public static void BuildIOSDevelopment()
        {
            var scenes = GetBuildScenes();
            var buildPath = GetBuildPath("iOS-Dev");

            BuildPlayer(scenes, buildPath, BuildTarget.iOS,
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        /// <summary>
        /// 全プラットフォームのビルドを実行
        /// </summary>
        [MenuItem("Build/Build All Platforms")]
        public static void BuildAll()
        {
            BuildWindows();
            BuildLinux();
            BuildMacOS();
            BuildWebGL();
            BuildAndroid();
            BuildIOS();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// ビルド対象シーンを取得
        /// EditorBuildSettings に登録されているシーンを優先、なければデフォルトシーンを使用
        /// </summary>
        private static string[] GetBuildScenes()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (enabledScenes.Length > 0)
            {
                Debug.Log($"[BuildScript] Using {enabledScenes.Length} scenes from EditorBuildSettings");
                return enabledScenes;
            }

            Debug.LogWarning("[BuildScript] No enabled scenes in EditorBuildSettings, using default scenes");
            return DefaultScenes;
        }

        /// <summary>
        /// ビルドを実行
        /// </summary>
        private static void BuildPlayer(string[] scenes, string locationPathName, BuildTarget target, BuildOptions options)
        {
            Debug.Log("========================================");
            Debug.Log($"[BuildScript] Starting build for {target}");
            Debug.Log($"[BuildScript] Output path: {locationPathName}");
            Debug.Log($"[BuildScript] Scenes ({scenes.Length}):");
            foreach (var scene in scenes)
            {
                Debug.Log($"  - {scene}");
            }
            Debug.Log($"[BuildScript] Options: {options}");

            // 環境シンボルを適用
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            BuildProfileHelper.ApplyEnvironmentSymbols(targetGroup);

            // Addressables の Local/Remote 設定を環境変数から適用
            AddressablesEnvironmentSwitcher.ApplyFromEnvironmentVariable();

            // Build Profile を確認（コマンドライン引数優先）
            var profilePath = BuildProfileHelper.GetBuildProfileFromArgs();
            if (string.IsNullOrEmpty(profilePath))
            {
                profilePath = BuildProfileHelper.FindBuildProfilePath(target);
            }

            if (!string.IsNullOrEmpty(profilePath))
            {
                Debug.Log($"[BuildScript] Using Build Profile: {profilePath}");
            }
            else
            {
                Debug.Log("[BuildScript] No Build Profile found, using default settings");
            }

            Debug.Log("========================================");

            // ビルドディレクトリを作成
            var directory = target == BuildTarget.WebGL
                ? locationPathName
                : Path.GetDirectoryName(locationPathName);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"[BuildScript] Created directory: {directory}");
            }

            // シーンの存在確認
            foreach (var scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    Debug.LogError($"[BuildScript] Scene not found: {scene}");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    return;
                }
            }

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                options = options
            };

            var startTime = DateTime.Now;
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var endTime = DateTime.Now;
            var summary = report.summary;

            Debug.Log("========================================");
            Debug.Log($"[BuildScript] Build Result: {summary.result}");
            Debug.Log($"[BuildScript] Build Time: {endTime - startTime}");

            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log($"[BuildScript] Build succeeded!");
                    Debug.Log($"[BuildScript] Total size: {FormatBytes(summary.totalSize)}");
                    Debug.Log($"[BuildScript] Total time: {summary.totalTime}");
                    Debug.Log($"[BuildScript] Warnings: {summary.totalWarnings}");
                    Debug.Log($"[BuildScript] Output: {summary.outputPath}");
                    break;

                case BuildResult.Failed:
                    Debug.LogError("[BuildScript] Build FAILED!");
                    LogBuildErrors(report);
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    break;

                case BuildResult.Cancelled:
                    Debug.LogWarning("[BuildScript] Build was cancelled");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(2);
                    }
                    break;

                case BuildResult.Unknown:
                    Debug.LogError("[BuildScript] Build result is unknown");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(3);
                    }
                    break;
            }
            Debug.Log("========================================");
        }

        /// <summary>
        /// ビルドエラーをログ出力
        /// </summary>
        private static void LogBuildErrors(BuildReport report)
        {
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    switch (message.type)
                    {
                        case LogType.Error:
                        case LogType.Exception:
                            Debug.LogError($"[{step.name}] {message.content}");
                            break;
                        case LogType.Warning:
                            Debug.LogWarning($"[{step.name}] {message.content}");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// バイト数を読みやすい形式にフォーマット
        /// </summary>
        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// ビルドフォルダをクリーンアップ
        /// </summary>
        [MenuItem("Build/Clean Build Folder")]
        public static void CleanBuildFolder()
        {
            if (Directory.Exists(BuildsPath))
            {
                Directory.Delete(BuildsPath, true);
                Debug.Log($"[BuildScript] Cleaned build folder: {BuildsPath}");
            }
            else
            {
                Debug.Log("[BuildScript] Build folder does not exist");
            }
        }

        /// <summary>
        /// 現在のビルド設定を表示
        /// </summary>
        [MenuItem("Build/Show Build Info")]
        public static void ShowBuildInfo()
        {
            Debug.Log("========================================");
            Debug.Log("[BuildScript] Build Information");
            Debug.Log($"  Product Name: {Application.productName}");
            Debug.Log($"  Company Name: {Application.companyName}");
            Debug.Log($"  Version: {Application.version}");
            Debug.Log($"  Unity Version: {Application.unityVersion}");
            Debug.Log($"  Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"  Builds Path: {BuildsPath}");
            Debug.Log("  Scenes:");
            foreach (var scene in GetBuildScenes())
            {
                Debug.Log($"    - {scene}");
            }
            Debug.Log("========================================");
        }

        #endregion
    }
}
