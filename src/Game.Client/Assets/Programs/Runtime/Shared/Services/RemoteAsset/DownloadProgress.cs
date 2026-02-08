namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// ダウンロードの状態を表す列挙型
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>ダウンロード未開始</summary>
        NotStarted,

        /// <summary>カタログ確認中</summary>
        Checking,

        /// <summary>ダウンロード中</summary>
        Downloading,

        /// <summary>ダウンロード完了</summary>
        Completed,

        /// <summary>ダウンロード失敗</summary>
        Failed
    }

    /// <summary>
    /// ダウンロードの進捗状態を表す構造体
    /// </summary>
    public readonly struct DownloadProgress
    {
        /// <summary>現在のダウンロード状態</summary>
        public DownloadStatus Status { get; init; }

        /// <summary>進捗率 (0.0 - 1.0)</summary>
        public float Progress { get; init; }

        /// <summary>ダウンロード済みバイト数</summary>
        public long DownloadedBytes { get; init; }

        /// <summary>総バイト数</summary>
        public long TotalBytes { get; init; }

        /// <summary>現在の操作内容 ("カタログ確認中" 等)</summary>
        public string CurrentOperation { get; init; }

        /// <summary>エラーメッセージ (失敗時のみ)</summary>
        public string ErrorMessage { get; init; }

        /// <summary>
        /// 未開始状態を作成
        /// </summary>
        public static DownloadProgress NotStarted() => new()
        {
            Status = DownloadStatus.NotStarted,
            Progress = 0f,
            CurrentOperation = string.Empty
        };

        /// <summary>
        /// 確認中状態を作成
        /// </summary>
        public static DownloadProgress Checking(string operation = "カタログ確認中...") => new()
        {
            Status = DownloadStatus.Checking,
            Progress = 0f,
            CurrentOperation = operation
        };

        /// <summary>
        /// ダウンロード中状態を作成
        /// </summary>
        public static DownloadProgress Downloading(float progress, long downloadedBytes, long totalBytes) => new()
        {
            Status = DownloadStatus.Downloading,
            Progress = progress,
            DownloadedBytes = downloadedBytes,
            TotalBytes = totalBytes,
            CurrentOperation = "ダウンロード中..."
        };

        /// <summary>
        /// 完了状態を作成
        /// </summary>
        public static DownloadProgress Completed() => new()
        {
            Status = DownloadStatus.Completed,
            Progress = 1f,
            CurrentOperation = "完了"
        };

        /// <summary>
        /// 失敗状態を作成
        /// </summary>
        public static DownloadProgress Failed(string errorMessage) => new()
        {
            Status = DownloadStatus.Failed,
            Progress = 0f,
            CurrentOperation = "失敗",
            ErrorMessage = errorMessage
        };
    }
}
