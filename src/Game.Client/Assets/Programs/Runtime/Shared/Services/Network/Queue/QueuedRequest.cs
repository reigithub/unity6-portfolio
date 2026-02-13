using System;

namespace Game.Shared.Services.Network.Queue
{
    /// <summary>
    /// リクエストの優先度
    /// </summary>
    public enum RequestPriority
    {
        /// <summary>低優先度（後回し）</summary>
        Low = 0,
        /// <summary>通常優先度</summary>
        Normal = 1,
        /// <summary>高優先度（優先的に処理）</summary>
        High = 2,
        /// <summary>最高優先度（即座に処理）</summary>
        Critical = 3
    }

    /// <summary>
    /// キューに入ったリクエストの状態
    /// </summary>
    public enum QueuedRequestState
    {
        /// <summary>待機中</summary>
        Pending,
        /// <summary>処理中</summary>
        Processing,
        /// <summary>完了</summary>
        Completed,
        /// <summary>失敗</summary>
        Failed,
        /// <summary>キャンセル済み</summary>
        Cancelled
    }

    /// <summary>
    /// キューに入ったリクエスト
    /// </summary>
    public class QueuedRequest
    {
        /// <summary>
        /// リクエストID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// APIエンドポイント
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// HTTPメソッド
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// JSONシリアライズされたリクエストボディ
        /// </summary>
        public string JsonBody { get; }

        /// <summary>
        /// レスポンスの型名（デシリアライズ用）
        /// </summary>
        public string ResponseTypeName { get; }

        /// <summary>
        /// 優先度
        /// </summary>
        public RequestPriority Priority { get; }

        /// <summary>
        /// キューに追加された時刻
        /// </summary>
        public DateTime QueuedAt { get; }

        /// <summary>
        /// リトライ回数
        /// </summary>
        public int RetryCount { get; internal set; }

        /// <summary>
        /// 最大リトライ回数
        /// </summary>
        public int MaxRetries { get; }

        /// <summary>
        /// 現在の状態
        /// </summary>
        public QueuedRequestState State { get; internal set; }

        /// <summary>
        /// 最後にエラーが発生した時刻
        /// </summary>
        public DateTime? LastErrorAt { get; internal set; }

        /// <summary>
        /// 最後のエラーメッセージ
        /// </summary>
        public string LastErrorMessage { get; internal set; }

        /// <summary>
        /// リクエストの有効期限
        /// </summary>
        public DateTime? ExpiresAt { get; }

        /// <summary>
        /// 有効期限切れかどうか
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// リトライ可能かどうか
        /// </summary>
        public bool CanRetry => RetryCount < MaxRetries && !IsExpired && State != QueuedRequestState.Cancelled;

        public QueuedRequest(
            string endpoint,
            string method,
            string jsonBody,
            string responseTypeName,
            RequestPriority priority = RequestPriority.Normal,
            int maxRetries = 3,
            TimeSpan? expiration = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Endpoint = endpoint;
            Method = method;
            JsonBody = jsonBody;
            ResponseTypeName = responseTypeName;
            Priority = priority;
            QueuedAt = DateTime.UtcNow;
            RetryCount = 0;
            MaxRetries = maxRetries;
            State = QueuedRequestState.Pending;
            ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null;
        }

        public override string ToString()
        {
            return $"[{Id}] {Method} {Endpoint} (Priority={Priority}, State={State}, Retries={RetryCount}/{MaxRetries})";
        }
    }
}
