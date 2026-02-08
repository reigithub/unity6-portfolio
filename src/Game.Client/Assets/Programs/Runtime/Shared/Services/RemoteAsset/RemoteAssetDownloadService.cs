using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// リモートアセットダウンロードサービスの実装
    /// Addressables API を使用してR2からアセットをダウンロード
    /// </summary>
    public class RemoteAssetDownloadService : IRemoteAssetDownloadService
    {
        private readonly GameEnvironment _environment;
        private DownloadProgress _currentProgress;
        private bool _isDownloaded;

        public bool IsDownloadRequired => _environment != GameEnvironment.Local && !_isDownloaded;
        public bool IsDownloaded => _isDownloaded;
        public DownloadProgress CurrentProgress => _currentProgress;

        public RemoteAssetDownloadService()
        {
            _environment = GameEnvironmentHelper.Current;
            _currentProgress = DownloadProgress.NotStarted();
            _isDownloaded = false;

            Debug.Log($"[RemoteAssetDownloadService] Environment: {_environment}, IsDownloadRequired: {IsDownloadRequired}");
        }

        public RemoteAssetDownloadService(GameEnvironment environment)
        {
            _environment = environment;
            _currentProgress = DownloadProgress.NotStarted();
            _isDownloaded = false;

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
                // Step 1: カタログ確認
                _currentProgress = DownloadProgress.Checking("カタログ確認中...");
                progress?.Report(_currentProgress);

                var catalogsToUpdate = await CheckForCatalogUpdatesAsync(cancellationToken);

                // Step 2: カタログ更新
                if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
                {
                    _currentProgress = DownloadProgress.Checking("カタログ更新中...");
                    progress?.Report(_currentProgress);

                    await UpdateCatalogsAsync(catalogsToUpdate, cancellationToken);
                }

                // Step 3: ダウンロードサイズ確認
                _currentProgress = DownloadProgress.Checking("ダウンロードサイズ確認中...");
                progress?.Report(_currentProgress);

                var downloadSize = await GetDownloadSizeAsync(cancellationToken);

                if (downloadSize <= 0)
                {
                    Debug.Log("[RemoteAssetDownloadService] No assets to download");
                    _isDownloaded = true;
                    _currentProgress = DownloadProgress.Completed();
                    progress?.Report(_currentProgress);
                    return true;
                }

                Debug.Log($"[RemoteAssetDownloadService] Download size: {FormatBytes(downloadSize)}");

                // Step 4: ダウンロード実行
                await DownloadDependenciesAsync(downloadSize, progress, cancellationToken);

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
                _currentProgress = DownloadProgress.NotStarted();

                // Addressables のリソースロケーターをクリア
                await Addressables.InitializeAsync();

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

        private async UniTask<long> GetDownloadSizeAsync(CancellationToken cancellationToken)
        {
            try
            {
                // すべてのAddressablesラベルに対してダウンロードサイズを取得
                var handle = Addressables.GetDownloadSizeAsync("default");
                await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var size = handle.Result;
                    Addressables.Release(handle);
                    return size;
                }

                Addressables.Release(handle);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoteAssetDownloadService] GetDownloadSize failed: {ex.Message}");
                return 0;
            }
        }

        private async UniTask DownloadDependenciesAsync(
            long totalBytes,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            var handle = Addressables.DownloadDependenciesAsync("default", false);

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
                        "ダウンロードに失敗しました");
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
