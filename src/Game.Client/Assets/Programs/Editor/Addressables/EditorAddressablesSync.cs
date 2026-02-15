#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Game.Shared;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Game.Editor.Addressables
{
    /// <summary>
    /// Addressablesローカルバンドルをリモートから同期するエディタ機能
    /// UseExistingBuildモードでチーム開発を円滑にする
    ///
    /// 同期方式: index.json
    /// - CIがLibrary/com.unity.addressables/全体をLocalBundles/としてR2にアップロード
    /// - index.jsonにファイル一覧とcatalogHashを含む
    /// - catalogHashの比較で同期要否を判断
    /// </summary>
    [InitializeOnLoad]
    public static class EditorAddressablesSync
    {
        private static readonly HttpClient HttpClient = new();
        private static bool _isSyncing;

        private const string IndexFileName = "index.json";
        private const string LocalBundlesFolder = "LocalBundles";

        static EditorAddressablesSync()
        {
            // バッチモード（CI）ではUseExistingBuildモードだとカタログ未存在で
            // AddressablesBuildScriptHooksがダイアログを表示しハングするため、
            // Play Mode ScriptをUse Asset Database（シミュレーション）に切り替える
            if (Application.isBatchMode)
            {
                SwitchToAssetDatabaseMode();
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Addressables Play Mode ScriptをUse Asset Database（インデックス0）に切り替える
        /// </summary>
        private static void SwitchToAssetDatabaseMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            const int assetDatabaseIndex = 0; // BuildScriptFastMode
            if (settings.ActivePlayModeDataBuilderIndex != assetDatabaseIndex)
            {
                settings.ActivePlayModeDataBuilderIndex = assetDatabaseIndex;
                Debug.Log("[AddressablesSync] Batch mode: Play Mode Script を Use Asset Database に切り替えました");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // UseExistingBuild モードでない場合はスキップ
                if (!ShouldAutoSync())
                {
                    return;
                }

                // Libraryにカタログが存在するかチェック
                if (!HasLocalCatalog())
                {
                    // Playモードを中止
                    EditorApplication.isPlaying = false;

                    // ダイアログを表示してダウンロードを促す
                    EditorApplication.delayCall += () =>
                    {
                        var result = EditorUtility.DisplayDialog(
                            "Addressables カタログが見つかりません",
                            "Library/com.unity.addressables にカタログが存在しません。\n" +
                            "UseExistingBuild モードで再生するには、リモートからカタログをダウンロードする必要があります。\n\n" +
                            "今すぐダウンロードしますか？",
                            "ダウンロード",
                            "キャンセル");

                        if (result)
                        {
                            DownloadAndPlayAsync();
                        }
                    };
                    return;
                }

                // カタログが存在する場合は通常の同期チェック
                CheckAndSyncAsync().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Debug.LogWarning($"[AddressablesSync] Auto-sync failed: {t.Exception.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// ダウンロード後にPlayモードを開始
        /// </summary>
        private static async void DownloadAndPlayAsync()
        {
            try
            {
                await CheckAndSyncAsync(forceSync: true);

                if (HasLocalCatalog())
                {
                    Debug.Log("[AddressablesSync] Download complete. Starting Play mode...");
                    EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "ダウンロード失敗",
                        "カタログのダウンロードに失敗しました。\n" +
                        "ネットワーク接続を確認してください。",
                        "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesSync] Download failed: {e.Message}");
                EditorUtility.DisplayDialog(
                    "ダウンロード失敗",
                    $"カタログのダウンロードに失敗しました。\n\n{e.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Libraryにカタログが存在するかチェック
        /// </summary>
        public static bool HasLocalCatalog()
        {
            var platform = GetPlatformFolder();
            var platformFolder = platform switch
            {
                "StandaloneWindows64" => "Windows",
                "StandaloneLinux64" => "Linux64",
                "StandaloneOSX" => "OSXUniversal",
                _ => platform
            };

            var catalogPath = Path.Combine(
                GetAddressablesLibraryBasePath(),
                "aa",
                platformFolder,
                "catalog.bin"
            );

            return File.Exists(catalogPath);
        }

        /// <summary>
        /// 自動同期が必要かどうかを判定
        /// - バッチモードでない
        /// - GameEnvironment が Local 以外
        /// - Play Mode Script が UseExistingBuild
        /// </summary>
        private static bool ShouldAutoSync()
        {
            // バッチモードではダイアログ表示・同期不可
            if (Application.isBatchMode)
            {
                return false;
            }

            // Local環境では同期不要（ローカルビルドを使用）
            if (GameEnvironmentHelper.Current == GameEnvironment.Local)
            {
                return false;
            }

            // UseExistingBuild モードかどうかを確認
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return false;
            }

            var activeBuilder = settings.ActivePlayModeDataBuilder;
            if (activeBuilder == null)
            {
                return false;
            }

            // BuildScriptPackedPlayMode = UseExistingBuild
            return activeBuilder.GetType().Name == "BuildScriptPackedPlayMode";
        }

        /// <summary>
        /// 自動同期が可能な状態かどうかを判定（UI用）
        /// </summary>
        public static bool CanSync()
        {
            if (GameEnvironmentHelper.Current == GameEnvironment.Local)
            {
                return false;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return false;
            }

            var activeBuilder = settings.ActivePlayModeDataBuilder;
            return activeBuilder?.GetType().Name == "BuildScriptPackedPlayMode";
        }

        [MenuItem("Tools/Addressables/Sync from Remote", priority = 100)]
        public static async void SyncFromRemoteMenu()
        {
            await CheckAndSyncAsync(forceSync: true);
        }

        [MenuItem("Tools/Addressables/Check Remote Version", priority = 101)]
        public static async void CheckRemoteVersionMenu()
        {
            await CheckAndSyncAsync(forceSync: false, dryRun: true);
        }

        /// <summary>
        /// リモートからLocalBundlesを同期
        /// </summary>
        /// <param name="forceSync">強制同期（ハッシュが同じでも同期）</param>
        /// <param name="dryRun">ドライラン（実際にはダウンロードしない）</param>
        public static async Task CheckAndSyncAsync(bool forceSync = false, bool dryRun = false)
        {
            if (_isSyncing)
            {
                Debug.Log("[AddressablesSync] Sync already in progress");
                return;
            }

            _isSyncing = true;
            try
            {
                var baseUrl = GetRemoteBaseUrl();
                if (string.IsNullOrEmpty(baseUrl))
                {
                    Debug.LogError("[AddressablesSync] Failed to get remote base URL from Addressables settings");
                    return;
                }

                var platform = GetPlatformFolder();

                // 1. index.json を取得
                var indexUrl = $"{baseUrl}/{platform}/{LocalBundlesFolder}/{IndexFileName}";
                Debug.Log($"[AddressablesSync] Fetching index from: {indexUrl}");
                var index = await FetchIndexAsync(indexUrl);

                if (index == null)
                {
                    Debug.LogWarning("[AddressablesSync] Failed to fetch remote index.json. Remote bundles may not be available.");
                    return;
                }

                // 2. ローカルの catalog.hash と比較
                var localHash = GetLocalCatalogHash();
                var needsSync = forceSync || localHash != index.CatalogHash;

                if (dryRun)
                {
                    Debug.Log($"[AddressablesSync] Remote catalog hash: {index.CatalogHash}");
                    Debug.Log($"[AddressablesSync] Local catalog hash: {localHash}");
                    Debug.Log($"[AddressablesSync] Needs sync: {needsSync}");
                    Debug.Log($"[AddressablesSync] Files count: {index.Files.Count}");
                    return;
                }

                if (!needsSync)
                {
                    Debug.Log("[AddressablesSync] Already up to date");
                    return;
                }

                Debug.Log($"[AddressablesSync] Syncing from remote (catalogHash: {index.CatalogHash})...");

                // 3. index.json に記載されたファイルをダウンロード
                var localBundlesUrl = $"{baseUrl}/{platform}/{LocalBundlesFolder}";
                var targetDir = GetAddressablesLibraryBasePath();

                await DownloadFilesAsync(localBundlesUrl, targetDir, index.Files);

                Debug.Log("[AddressablesSync] Sync completed successfully");

                // アセットデータベースを更新
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesSync] Sync failed: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static async Task<LocalBundlesIndex> FetchIndexAsync(string url)
        {
            try
            {
                var json = await HttpClient.GetStringAsync(url);
                return JsonUtility.FromJson<LocalBundlesIndex>(json);
            }
            catch (HttpRequestException e)
            {
                Debug.LogWarning($"[AddressablesSync] Failed to fetch index.json: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AddressablesSync] Failed to parse index.json: {e.Message}");
                return null;
            }
        }

        private static async Task DownloadFilesAsync(string baseUrl, string targetDir, List<string> files)
        {
            var totalFiles = files.Count;
            var downloadedFiles = 0;
            var skippedFiles = 0;
            var failedFiles = 0;

            foreach (var relativePath in files)
            {
                try
                {
                    var url = $"{baseUrl}/{relativePath}";
                    var targetPath = Path.Combine(targetDir, relativePath);

                    // ディレクトリを作成
                    var directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // ダウンロード
                    await DownloadFileAsync(url, targetPath);
                    downloadedFiles++;

                    // 進捗表示（10ファイルごと）
                    if (downloadedFiles % 10 == 0)
                    {
                        Debug.Log($"[AddressablesSync] Progress: {downloadedFiles + skippedFiles}/{totalFiles} files");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AddressablesSync] Failed to download {relativePath}: {e.Message}");
                    failedFiles++;
                }
            }

            Debug.Log($"[AddressablesSync] Download complete: {downloadedFiles} downloaded, {skippedFiles} skipped, {failedFiles} failed (total: {totalFiles})");
        }

        private static async Task DownloadFileAsync(string url, string targetPath)
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(targetPath, bytes);
        }

        /// <summary>
        /// Addressables設定からリモートベースURLを取得
        /// Content.LoadPath から [BuildTarget] を除去してベースURLを取得
        /// </summary>
        private static string GetRemoteBaseUrl()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[AddressablesSync] AddressableAssetSettings not found");
                return null;
            }

            // Content.LoadPath を取得（例: "https://develop.assets.rei-unity6-portfolio.com/[BuildTarget]"）
            var contentLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Content.LoadPath")
                                  ?? settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");

            if (string.IsNullOrEmpty(contentLoadPath))
            {
                Debug.LogWarning("[AddressablesSync] Content.LoadPath not configured in Addressables Profile");
                return null;
            }

            // [BuildTarget] 部分を除去してベースURLを取得
            var baseUrl = contentLoadPath
                .Replace("/[BuildTarget]", "")
                .Replace("[BuildTarget]", "")
                .TrimEnd('/');

            Debug.Log($"[AddressablesSync] Remote base URL: {baseUrl} (from Profile: {settings.profileSettings.GetProfileName(settings.activeProfileId)})");
            return baseUrl;
        }

        /// <summary>
        /// Addressables設定からプラットフォームフォルダを取得
        /// </summary>
        private static string GetPlatformFolder()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                // BuildTarget 変数を評価
                var buildTarget = settings.profileSettings.GetValueByName(settings.activeProfileId, "BuildTarget");
                if (!string.IsNullOrEmpty(buildTarget))
                {
                    var evaluated = settings.profileSettings.EvaluateString(settings.activeProfileId, buildTarget);
                    if (!string.IsNullOrEmpty(evaluated))
                    {
                        return evaluated;
                    }
                }
            }

            // フォールバック: EditorUserBuildSettings から取得
            return EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.StandaloneWindows64 => "StandaloneWindows64",
                BuildTarget.StandaloneOSX => "StandaloneOSX",
                BuildTarget.iOS => "iOS",
                BuildTarget.Android => "Android",
                BuildTarget.WebGL => "WebGL",
                _ => "StandaloneWindows64"
            };
        }

        /// <summary>
        /// Library/com.unity.addressables/ のベースパスを取得
        /// </summary>
        private static string GetAddressablesLibraryBasePath()
        {
            return Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "com.unity.addressables"
            );
        }

        /// <summary>
        /// ローカルのcatalog.hashを取得
        /// </summary>
        private static string GetLocalCatalogHash()
        {
            var platform = GetPlatformFolder();

            // プラットフォームフォルダ名の変換（StandaloneWindows64 → Windows）
            var platformFolder = platform switch
            {
                "StandaloneWindows64" => "Windows",
                "StandaloneLinux64" => "Linux64",
                "StandaloneOSX" => "OSXUniversal",
                _ => platform
            };

            var hashPath = Path.Combine(
                GetAddressablesLibraryBasePath(),
                "aa",
                platformFolder,
                "catalog.hash"
            );

            if (File.Exists(hashPath))
            {
                return File.ReadAllText(hashPath).Trim();
            }

            return "";
        }
    }

    /// <summary>
    /// index.json のデータ構造
    /// </summary>
    [Serializable]
    public class LocalBundlesIndex
    {
        public string catalogHash;
        public List<string> files;

        // プロパティアクセサ
        public string CatalogHash => catalogHash;
        public List<string> Files => files ?? new List<string>();
    }
}
#endif
