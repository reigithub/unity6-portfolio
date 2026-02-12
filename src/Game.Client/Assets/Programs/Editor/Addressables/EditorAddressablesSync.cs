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
    /// </summary>
    [InitializeOnLoad]
    public static class EditorAddressablesSync
    {
        private static readonly HttpClient HttpClient = new();
        private static bool _isSyncing;

        private const string ManifestFileName = "local_bundles_manifest.json";
        private const string LocalHashCacheKey = "AddressablesSync_LocalCatalogHash";

        static EditorAddressablesSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Play開始前に同期チェック
                if (ShouldAutoSync())
                {
                    CheckAndSyncAsync().ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            Debug.LogWarning($"[AddressablesSync] Auto-sync failed: {t.Exception.Message}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 自動同期が必要かどうかを判定
        /// - GameEnvironment が Local 以外
        /// - Play Mode Script が UseExistingBuild
        /// </summary>
        private static bool ShouldAutoSync()
        {
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

                // マニフェスト取得
                var manifestUrl = $"{baseUrl}/{platform}/{ManifestFileName}";
                var manifest = await FetchManifestAsync(manifestUrl);

                if (manifest == null)
                {
                    Debug.LogWarning("[AddressablesSync] Failed to fetch remote manifest");
                    return;
                }

                // ローカルハッシュと比較
                var localHash = GetLocalCatalogHash();
                var needsSync = forceSync || localHash != manifest.CatalogHash;

                if (dryRun)
                {
                    Debug.Log($"[AddressablesSync] Remote version: {manifest.Version}");
                    Debug.Log($"[AddressablesSync] Remote catalog hash: {manifest.CatalogHash}");
                    Debug.Log($"[AddressablesSync] Local catalog hash: {localHash}");
                    Debug.Log($"[AddressablesSync] Needs sync: {needsSync}");
                    Debug.Log($"[AddressablesSync] Local bundles count: {manifest.LocalBundles.Count}");
                    return;
                }

                if (!needsSync)
                {
                    Debug.Log("[AddressablesSync] Already up to date");
                    return;
                }

                Debug.Log($"[AddressablesSync] Syncing from remote (version: {manifest.Version})...");

                // カタログをダウンロード
                await DownloadCatalogAsync(baseUrl, platform);

                // ローカルバンドルをダウンロード
                await DownloadBundlesAsync(baseUrl, platform, manifest.LocalBundles);

                // ハッシュをキャッシュ
                EditorPrefs.SetString(LocalHashCacheKey, manifest.CatalogHash);

                Debug.Log("[AddressablesSync] Sync completed successfully");

                // アセットデータベースを更新
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesSync] Sync failed: {e.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static async Task<LocalBundlesManifest> FetchManifestAsync(string url)
        {
            try
            {
                var json = await HttpClient.GetStringAsync(url);
                return JsonUtility.FromJson<LocalBundlesManifest>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AddressablesSync] Failed to fetch manifest: {e.Message}");
                return null;
            }
        }

        private static async Task DownloadCatalogAsync(string baseUrl, string platform)
        {
            var targetDir = GetAddressablesLibraryPath(platform);
            Directory.CreateDirectory(targetDir);

            var files = new[] { "catalog.bin", "catalog.hash" };
            foreach (var file in files)
            {
                // catalog_0.1.0.bin → catalog.bin にリネーム
                var remoteFile = file.Replace("catalog.", $"catalog_{PlayerSettings.bundleVersion}.");
                var url = $"{baseUrl}/{platform}/{remoteFile}";
                var targetPath = Path.Combine(targetDir, file);

                await DownloadFileAsync(url, targetPath);
            }
        }

        private static async Task DownloadBundlesAsync(string baseUrl, string platform, List<LocalBundleInfo> bundles)
        {
            var targetDir = GetAddressablesLibraryPath(platform);

            foreach (var bundle in bundles)
            {
                // バンドルはフラットに配置されているため、パスからファイル名を取得
                var url = $"{baseUrl}/{platform}/{Path.GetFileName(bundle.Path)}";
                var targetPath = Path.Combine(targetDir, bundle.Path);

                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // ハッシュが同じならスキップ
                if (File.Exists(targetPath))
                {
                    var localHash = ComputeFileHash(targetPath);
                    if (localHash == bundle.Hash)
                    {
                        Debug.Log($"[AddressablesSync] Skipping (unchanged): {bundle.Path}");
                        continue;
                    }
                }

                await DownloadFileAsync(url, targetPath);
                Debug.Log($"[AddressablesSync] Downloaded: {bundle.Path}");
            }
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

        private static string GetAddressablesLibraryPath(string platform)
        {
            return Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "com.unity.addressables",
                "aa",
                platform == "StandaloneWindows64" ? "Windows" : platform
            );
        }

        private static string GetLocalCatalogHash()
        {
            var platform = GetPlatformFolder();
            var hashPath = Path.Combine(GetAddressablesLibraryPath(platform), "catalog.hash");

            if (File.Exists(hashPath))
            {
                return File.ReadAllText(hashPath).Trim();
            }

            return EditorPrefs.GetString(LocalHashCacheKey, "");
        }

        private static string ComputeFileHash(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    [Serializable]
    public class LocalBundlesManifest
    {
        public string version;
        public string buildTime;
        public string catalogHash;
        public List<LocalBundleInfo> localBundles;

        // プロパティアクセサ（互換性用）
        public string Version => version;
        public string BuildTime => buildTime;
        public string CatalogHash => catalogHash;
        public List<LocalBundleInfo> LocalBundles => localBundles;
    }

    [Serializable]
    public class LocalBundleInfo
    {
        public string path;
        public string hash;
        public long size;

        // プロパティアクセサ（互換性用）
        public string Path => path;
        public string Hash => hash;
        public long Size => size;
    }
}
#endif
