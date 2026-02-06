using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor.Build
{
    /// <summary>
    /// Addressables を Cloudflare R2 にアップロードするためのエディタツール
    /// </summary>
    public static class AddressablesR2Uploader
    {
        /// <summary>
        /// R2 バケット名
        /// </summary>
        private const string BucketName = "unity6-portfolio";

        /// <summary>
        /// rclone リモート名
        /// </summary>
        private const string RcloneRemote = "r2";

        /// <summary>
        /// カスタムドメイン
        /// </summary>
        private const string CustomDomain = "rei-unity6-portfolio.com";

        /// <summary>
        /// ServerData フォルダのパス
        /// </summary>
        private static string ServerDataPath => Path.Combine(Application.dataPath, "..", "ServerData");

        #region Menu Items

        [MenuItem("Build/Addressables/Build Only", priority = 100)]
        public static void BuildOnly()
        {
            if (BuildAddressables())
            {
                Debug.Log("<color=green>[Addressables] ビルド完了</color>");
            }
        }

        [MenuItem("Build/Addressables/Build and Upload to R2", priority = 101)]
        public static void BuildAndUpload()
        {
            if (!BuildAddressables())
            {
                Debug.LogError("[Addressables] ビルド失敗。アップロードをキャンセルしました。");
                return;
            }

            UploadToR2();
        }

        [MenuItem("Build/Addressables/Upload to R2 (Without Build)", priority = 102)]
        public static void UploadOnly()
        {
            UploadToR2();
        }

        [MenuItem("Build/Addressables/Upload to R2 (Dry Run)", priority = 103)]
        public static void UploadDryRun()
        {
            UploadToR2(dryRun: true);
        }

        [MenuItem("Build/Addressables/Clean ServerData", priority = 200)]
        public static void CleanServerData()
        {
            if (Directory.Exists(ServerDataPath))
            {
                Directory.Delete(ServerDataPath, recursive: true);
                Debug.Log($"[Addressables] ServerData フォルダを削除しました: {ServerDataPath}");
            }
            else
            {
                Debug.Log("[Addressables] ServerData フォルダは存在しません");
            }
        }

        [MenuItem("Build/Addressables/Open R2 Dashboard", priority = 300)]
        public static void OpenR2Dashboard()
        {
            Application.OpenURL("https://dash.cloudflare.com/?to=/:account/r2/overview");
        }

        [MenuItem("Build/Addressables/Open Remote URL", priority = 301)]
        public static void OpenRemoteUrl()
        {
            var platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            Application.OpenURL($"https://{CustomDomain}/{platform}/");
        }

        [MenuItem("Build/Addressables/Show Build Info", priority = 302)]
        public static void ShowBuildInfo()
        {
            Debug.Log("========================================");
            Debug.Log("[Addressables] Build Information");
            Debug.Log($"  Platform: {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"  ServerData Path: {ServerDataPath}");
            Debug.Log($"  R2 Bucket: {BucketName}");
            Debug.Log($"  Remote URL: https://{CustomDomain}/");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                Debug.Log($"  Active Profile: {settings.profileSettings.GetProfileName(settings.activeProfileId)}");
                Debug.Log($"  Build Remote Catalog: {settings.BuildRemoteCatalog}");
            }

            // ServerData の内容を表示
            if (Directory.Exists(ServerDataPath))
            {
                var platforms = Directory.GetDirectories(ServerDataPath);
                Debug.Log($"  Built Platforms ({platforms.Length}):");
                foreach (var platform in platforms)
                {
                    var dirInfo = new DirectoryInfo(platform);
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    var totalSize = files.Sum(f => f.Length);
                    var sizeMB = totalSize / (1024.0 * 1024.0);
                    Debug.Log($"    - {dirInfo.Name}: {files.Length} files, {sizeMB:F2} MB");
                }
            }
            else
            {
                Debug.Log("  Built Platforms: (none)");
            }

            Debug.Log("========================================");
        }

        #endregion

        #region Build Methods

        /// <summary>
        /// Addressables をビルド
        /// </summary>
        /// <returns>ビルド成功時は true</returns>
        private static bool BuildAddressables()
        {
            Debug.Log("[Addressables] ビルド開始...");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] AddressableAssetSettings が見つかりません");
                return false;
            }

            // 古いビルドをクリーン
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);

            // ビルド実行
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[Addressables] ビルドエラー: {result.Error}");
                return false;
            }

            Debug.Log($"[Addressables] ビルド完了: {result.OutputPath}");
            Debug.Log($"[Addressables] Duration: {result.Duration:F2}s");

            return true;
        }

        /// <summary>
        /// CI環境からコマンドラインで呼び出すビルドメソッド
        /// Unity -executeMethod Game.Editor.Build.AddressablesR2Uploader.BuildAddressablesCI
        ///
        /// 環境変数:
        ///   ADDRESSABLES_PROFILE: 使用するプロファイル名（デフォルト: Default）
        ///   ADDRESSABLES_REMOTE_LOAD_PATH: リモートロードパス（オプション）
        /// </summary>
        public static void BuildAddressablesCI()
        {
            Debug.Log("========================================");
            Debug.Log("[Addressables] CI ビルド開始");
            Debug.Log("========================================");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] AddressableAssetSettings が見つかりません");
                EditorApplication.Exit(1);
                return;
            }

            // ビルド情報を出力
            Debug.Log($"[Addressables] Unity Version: {Application.unityVersion}");
            Debug.Log($"[Addressables] Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"[Addressables] ServerData Path: {ServerDataPath}");

            // プロファイル設定
            var profileName = GetEnvironmentVariable("ADDRESSABLES_PROFILE", "Default");
            var profileId = settings.profileSettings.GetProfileId(profileName);
            if (!string.IsNullOrEmpty(profileId))
            {
                settings.activeProfileId = profileId;
                Debug.Log($"[Addressables] Profile: {profileName}");
            }
            else
            {
                Debug.LogWarning($"[Addressables] Profile '{profileName}' not found, using current profile");
            }

            // リモートロードパスの設定（環境変数でオーバーライド可能）
            var remoteLoadPath = GetEnvironmentVariable("ADDRESSABLES_REMOTE_LOAD_PATH", null);
            if (!string.IsNullOrEmpty(remoteLoadPath))
            {
                Debug.Log($"[Addressables] Remote Load Path override: {remoteLoadPath}");
                // プロファイル変数を更新
                var remoteLoadPathId = settings.profileSettings.GetProfileDataByName("Remote.LoadPath");
                if (remoteLoadPathId != null)
                {
                    settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", remoteLoadPath);
                }
            }

            // Build Remote Catalog を有効化（CI では常に有効）
            if (!settings.BuildRemoteCatalog)
            {
                Debug.Log("[Addressables] Enabling Build Remote Catalog for CI build");
                settings.BuildRemoteCatalog = true;
            }

            // 現在の設定を出力
            Debug.Log($"[Addressables] Build Remote Catalog: {settings.BuildRemoteCatalog}");
            Debug.Log($"[Addressables] Active Profile: {settings.profileSettings.GetProfileName(settings.activeProfileId)}");

            var remoteBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath");
            var remoteLoadPathValue = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");
            Debug.Log($"[Addressables] Remote.BuildPath: {remoteBuildPath}");
            Debug.Log($"[Addressables] Remote.LoadPath: {remoteLoadPathValue}");

            // 古いビルドをクリーン
            Debug.Log("[Addressables] Cleaning previous build...");
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);

            // ビルド実行
            Debug.Log("[Addressables] Building...");
            var startTime = DateTime.Now;
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            var buildTime = DateTime.Now - startTime;

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError("========================================");
                Debug.LogError($"[Addressables] ビルドエラー: {result.Error}");
                Debug.LogError("========================================");
                EditorApplication.Exit(1);
                return;
            }

            // ビルド結果を出力
            Debug.Log("========================================");
            Debug.Log("[Addressables] CI ビルド完了!");
            Debug.Log("========================================");
            Debug.Log($"[Addressables] Output Path: {result.OutputPath}");
            Debug.Log($"[Addressables] Duration: {result.Duration:F2}s (Total: {buildTime.TotalSeconds:F2}s)");
            Debug.Log($"[Addressables] Location Count: {result.LocationCount}");

            // 出力ファイルを確認
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            var outputPath = Path.Combine(ServerDataPath, buildTarget);
            if (Directory.Exists(outputPath))
            {
                var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
                var totalSize = files.Sum(f => new FileInfo(f).Length);
                var sizeMB = totalSize / (1024.0 * 1024.0);
                Debug.Log($"[Addressables] Output Files: {files.Length}");
                Debug.Log($"[Addressables] Total Size: {sizeMB:F2} MB");

                // カタログファイルの確認
                var catalogFiles = files.Where(f => f.Contains("catalog")).ToArray();
                if (catalogFiles.Length > 0)
                {
                    Debug.Log("[Addressables] Catalog files:");
                    foreach (var file in catalogFiles)
                    {
                        Debug.Log($"  - {Path.GetFileName(file)}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Addressables] Output directory not found: {outputPath}");
            }

            Debug.Log("========================================");
        }

        private static string GetEnvironmentVariable(string name, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// コマンドライン引数を取得
        /// </summary>
        private static string GetCommandLineArg(string name, string defaultValue = null)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == $"-{name}" || args[i] == $"--{name}")
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }

        #endregion

        #region Upload Methods

        /// <summary>
        /// R2 にアップロード
        /// </summary>
        /// <param name="dryRun">ドライラン（実際にはアップロードしない）</param>
        private static void UploadToR2(bool dryRun = false)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            var platformPath = Path.Combine(ServerDataPath, buildTarget);

            if (!Directory.Exists(platformPath))
            {
                Debug.LogError($"[Addressables] ビルド出力が見つかりません: {platformPath}");
                Debug.LogError("[Addressables] 先に Addressables をビルドしてください");
                return;
            }

            // ファイル情報を表示
            var files = Directory.GetFiles(platformPath, "*", SearchOption.AllDirectories);
            var totalSize = files.Sum(f => new FileInfo(f).Length);
            var sizeMB = totalSize / (1024.0 * 1024.0);
            Debug.Log($"[Addressables] アップロード対象: {files.Length} files, {sizeMB:F2} MB");

            if (dryRun)
            {
                Debug.Log("[Addressables] <color=yellow>DRY RUN モード - 実際にはアップロードしません</color>");
            }

            Debug.Log($"[Addressables] R2 にアップロード中: {platformPath}");

            var destination = $"{RcloneRemote}:{BucketName}/{buildTarget}";

            var arguments = $"sync \"{platformPath}\" \"{destination}\" --progress --stats-one-line";
            if (dryRun)
            {
                arguments += " --dry-run";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Debug.LogError("[Addressables] rclone プロセスの開始に失敗しました");
                    Debug.LogError("rclone がインストールされているか確認してください: winget install Rclone.Rclone");
                    return;
                }

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        Debug.Log($"[rclone] {args.Data}");
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        Debug.LogWarning($"[rclone] {args.Data}");
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    if (dryRun)
                    {
                        Debug.Log("<color=yellow>[Addressables] DRY RUN 完了</color>");
                    }
                    else
                    {
                        Debug.Log("<color=green>[Addressables] アップロード完了!</color>");
                        Debug.Log($"[Addressables] URL: https://{CustomDomain}/{buildTarget}/");
                    }
                }
                else
                {
                    Debug.LogError($"[Addressables] アップロード失敗 (Exit code: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Addressables] アップロードエラー: {ex.Message}");
                Debug.LogError("rclone がインストールされているか確認してください: winget install Rclone.Rclone");
            }
        }

        #endregion
    }
}
