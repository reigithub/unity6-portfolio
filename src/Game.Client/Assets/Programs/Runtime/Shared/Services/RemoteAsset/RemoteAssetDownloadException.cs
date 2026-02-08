using System;

namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// リモートアセットダウンロード時の例外
    /// </summary>
    public class RemoteAssetDownloadException : Exception
    {
        /// <summary>失敗したダウンロード操作</summary>
        public string Operation { get; }

        /// <summary>リトライ回数</summary>
        public int RetryCount { get; }

        public RemoteAssetDownloadException(string message)
            : base(message)
        {
        }

        public RemoteAssetDownloadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public RemoteAssetDownloadException(string operation, string message)
            : base(message)
        {
            Operation = operation;
        }

        public RemoteAssetDownloadException(string operation, string message, Exception innerException)
            : base(message, innerException)
        {
            Operation = operation;
        }

        public RemoteAssetDownloadException(string operation, string message, int retryCount)
            : base(message)
        {
            Operation = operation;
            RetryCount = retryCount;
        }

        public RemoteAssetDownloadException(string operation, string message, Exception innerException, int retryCount)
            : base(message, innerException)
        {
            Operation = operation;
            RetryCount = retryCount;
        }
    }
}
