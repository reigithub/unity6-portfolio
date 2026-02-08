using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// リモートアセットダウンロードサービスのインターフェース
    /// </summary>
    public interface IRemoteAssetDownloadService
    {
        /// <summary>
        /// ダウンロードが必要かどうか（Remote環境かつ未ダウンロードの場合true）
        /// </summary>
        bool IsDownloadRequired { get; }

        /// <summary>
        /// アセットがダウンロード済みかどうか
        /// </summary>
        bool IsDownloaded { get; }

        /// <summary>
        /// 現在のダウンロード進捗
        /// </summary>
        DownloadProgress CurrentProgress { get; }

        /// <summary>
        /// アセットをダウンロードする
        /// </summary>
        /// <param name="progress">進捗報告用のIProgress</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合true</returns>
        UniTask<bool> DownloadAssetsAsync(
            IProgress<DownloadProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ダウンロードキャッシュをクリアする
        /// </summary>
        UniTask ClearCacheAsync();
    }
}
