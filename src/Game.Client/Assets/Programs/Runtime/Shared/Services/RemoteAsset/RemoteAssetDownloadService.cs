using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// リモートアセットダウンロードサービスの実装
    /// Addressables API を使用してR2からアセットをダウンロード
    /// 全リモートアセットを自動検出してダウンロード
    /// </summary>
    public class RemoteAssetDownloadService : IRemoteAssetDownloadService
    {
        private readonly GameEnvironment _environment;
        private DownloadProgress _currentProgress;
        private bool _isDownloaded;
        private List<object> _downloadKeys;

        public bool IsDownloadRequired => _environment != GameEnvironment.Local && !_isDownloaded;
        public bool IsDownloaded => _isDownloaded;
        public DownloadProgress CurrentProgress => _currentProgress;

        public RemoteAssetDownloadService()
        {
            _environment = GameEnvironmentHelper.Current;
            _currentProgress = DownloadProgress.NotStarted();
            _isDownloaded = false;
            _downloadKeys = new List<object>();

            Debug.Log($"[RemoteAssetDownloadService] Environment: {_environment}, IsDownloadRequired: {IsDownloadRequired}");
        }

        public RemoteAssetDownloadService(GameEnvironment environment)
        {
            _environment = environment;
            _currentProgress = DownloadProgress.NotStarted();
            _isDownloaded = false;
            _downloadKeys = new List<object>();

            Debug.Log($"[RemoteAssetDownloadService] Environment: {_environment}, IsDownloadRequired: {IsDownloadRequired}");
        }

        public async UniTask<bool> DownloadAssetsAsync(
            IProgress<DownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_environment == GameEnvironment.Local)
            {
                Debug.Log("[RemoteAssetDownloadService] Local environment - skipping download");
                _isDownloaded = true;
                _currentProgress = DownloadProgress.Completed();
                progress?.Report(_currentProgress);
                return true;
            }

            if (_isDownloaded)
            {
                Debug.Log("[RemoteAssetDownloadService] Already downloaded - skipping");
                _currentProgress = DownloadProgress.Completed();
                progress?.Report(_currentProgress);
                return true;
            }

            try
            {
                // Step 1: Addressables 初期化
                _currentProgress = DownloadProgress.Checking("初期化中...");
                progress?.Report(_currentProgress);

                await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);

                // Step 2: カタログ確認・更新
                _currentProgress = DownloadProgress.Checking("カタログ確認中...");
                progress?.Report(_currentProgress);

                var catalogsToUpdate = await CheckForCatalogUpdatesAsync(cancellationToken);

                if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
                {
                    _currentProgress = DownloadProgress.Checking("カタログ更新中...");
                    progress?.Report(_currentProgress);

                    await UpdateCatalogsAsync(catalogsToUpdate, cancellationToken);
                }

                // Step 3: 全リモートアセットのキーを収集
                _currentProgress = DownloadProgress.Checking("アセット確認中...");
                progress?.Report(_currentProgress);

                _downloadKeys = await CollectRemoteAssetKeysAsync(cancellationToken);

                if (_downloadKeys.Count == 0)
                {
                    Debug.Log("[RemoteAssetDownloadService] No remote assets found");
                    _isDownloaded = true;
                    _currentProgress = DownloadProgress.Completed();
                    progress?.Report(_currentProgress);
                    return true;
                }

                Debug.Log($"[RemoteAssetDownloadService] Found {_downloadKeys.Count} remote asset keys");

                // Step 4: 総ダウンロードサイズを取得
                _currentProgress = DownloadProgress.Checking("ダウンロードサイズ確認中...");
                progress?.Report(_currentProgress);

                var totalSize = await GetTotalDownloadSizeAsync(_downloadKeys, cancellationToken);

                if (totalSize <= 0)
                {
                    Debug.Log("[RemoteAssetDownloadService] All assets already cached");
                    _isDownloaded = true;
                    _currentProgress = DownloadProgress.Completed();
                    progress?.Report(_currentProgress);
                    return true;
                }

                Debug.Log($"[RemoteAssetDownloadService] Total download size: {FormatBytes(totalSize)}");

                // Step 5: ダウンロード実行
                await DownloadAllAssetsAsync(_downloadKeys, totalSize, progress, cancellationToken);

                _isDownloaded = true;
                _currentProgress = DownloadProgress.Completed();
                progress?.Report(_currentProgress);

                Debug.Log("[RemoteAssetDownloadService] Download completed successfully");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[RemoteAssetDownloadService] Download cancelled");
                _currentProgress = DownloadProgress.Failed("ダウンロードがキャンセルされました");
                progress?.Report(_currentProgress);
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteAssetDownloadService] Download failed: {ex.Message}");
                _currentProgress = DownloadProgress.Failed(ex.Message);
                progress?.Report(_currentProgress);
                throw new RemoteAssetDownloadException("DownloadAssets", ex.Message, ex);
            }
        }

        public async UniTask ClearCacheAsync()
        {
            Debug.Log("[RemoteAssetDownloadService] Clearing cache...");

            try
            {
                Caching.ClearCache();
                _isDownloaded = false;
                _downloadKeys.Clear();
                _currentProgress = DownloadProgress.NotStarted();

                await Addressables.InitializeAsync().ToUniTask();

                Debug.Log("[RemoteAssetDownloadService] Cache cleared");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteAssetDownloadService] Failed to clear cache: {ex.Message}");
                throw new RemoteAssetDownloadException("ClearCache", ex.Message, ex);
            }
        }

        private async UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.CheckForCatalogUpdates(false);
                await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var catalogs = handle.Result;
                    Debug.Log($"[RemoteAssetDownloadService] Found {catalogs?.Count ?? 0} catalog(s) to update");
                    Addressables.Release(handle);
                    return catalogs;
                }

                Addressables.Release(handle);
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoteAssetDownloadService] Catalog check failed: {ex.Message}");
                return null;
            }
        }

        private async UniTask UpdateCatalogsAsync(List<string> catalogs, CancellationToken cancellationToken)
        {
            try
            {
                var handle = Addressables.UpdateCatalogs(catalogs, false);
                await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log("[RemoteAssetDownloadService] Catalogs updated successfully");
                }
                else
                {
                    Debug.LogWarning("[RemoteAssetDownloadService] Catalog update failed");
                }

                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteAssetDownloadService] Catalog update error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 全リモートアセットのキーを収集
        /// </summary>
        private async UniTask<List<object>> CollectRemoteAssetKeysAsync(CancellationToken cancellationToken)
        {
            var remoteKeys = new HashSet<object>();

            await UniTask.SwitchToMainThread(cancellationToken);

            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (var key in locator.Keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // GUID形式のキーはスキップ（アドレスのみを対象）
                    if (key is string keyStr && Guid.TryParse(keyStr, out _))
                    {
                        continue;
                    }

                    // ロケーションを取得してリモートかどうか判定
                    if (locator.Locate(key, typeof(object), out var locations))
                    {
                        foreach (var location in locations)
                        {
                            if (IsRemoteLocation(location))
                            {
                                remoteKeys.Add(key);
                                break;
                            }
                        }
                    }
                }
            }

            return remoteKeys.ToList();
        }

        /// <summary>
        /// ロケーションがリモートかどうかを判定
        /// </summary>
        private bool IsRemoteLocation(IResourceLocation location)
        {
            if (location == null) return false;

            var internalId = location.InternalId;

            // HTTP/HTTPS で始まる場合はリモート
            if (internalId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                internalId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 依存関係もチェック
            if (location.HasDependencies)
            {
                foreach (var dep in location.Dependencies)
                {
                    if (IsRemoteLocation(dep))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 全キーの合計ダウンロードサイズを取得
        /// </summary>
        private async UniTask<long> GetTotalDownloadSizeAsync(List<object> keys, CancellationToken cancellationToken)
        {
            long totalSize = 0;

            try
            {
                var handle = Addressables.GetDownloadSizeAsync(keys);
                await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    totalSize = handle.Result;
                }

                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoteAssetDownloadService] GetDownloadSize failed: {ex.Message}");
            }

            return totalSize;
        }

        /// <summary>
        /// 全アセットをダウンロード
        /// </summary>
        private async UniTask DownloadAllAssetsAsync(
            List<object> keys,
            long totalBytes,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            var handle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);

            try
            {
                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var downloadStatus = handle.GetDownloadStatus();
                    var downloadedBytes = (long)(downloadStatus.Percent * totalBytes);

                    _currentProgress = DownloadProgress.Downloading(
                        downloadStatus.Percent,
                        downloadedBytes,
                        totalBytes);
                    progress?.Report(_currentProgress);

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new RemoteAssetDownloadException(
                        "DownloadDependencies",
                        $"ダウンロードに失敗しました: {handle.OperationException?.Message}");
                }
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
