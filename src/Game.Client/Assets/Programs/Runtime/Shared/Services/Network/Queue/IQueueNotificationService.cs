using System;
using R3;

namespace Game.Shared.Services.Network.Queue
{
    /// <summary>
    /// キュー状態通知の種類
    /// </summary>
    public enum QueueNotificationType
    {
        /// <summary>リクエストがキューに追加された</summary>
        RequestQueued,
        /// <summary>リクエストが完了した</summary>
        RequestCompleted,
        /// <summary>リクエストが失敗した（リトライ予定）</summary>
        RequestRetrying,
        /// <summary>リクエストが永続的に失敗した</summary>
        RequestFailed,
        /// <summary>キューの処理が開始された</summary>
        ProcessingStarted,
        /// <summary>キューの処理が完了した</summary>
        ProcessingCompleted,
        /// <summary>キューがクリアされた</summary>
        QueueCleared
    }

    /// <summary>
    /// キュー状態通知データ
    /// </summary>
    public sealed class QueueNotification
    {
        /// <summary>通知の種類</summary>
        public QueueNotificationType Type { get; }

        /// <summary>対象リクエストのID（該当する場合）</summary>
        public string RequestId { get; }

        /// <summary>対象リクエストのエンドポイント（該当する場合）</summary>
        public string Endpoint { get; }

        /// <summary>現在の保留中リクエスト数</summary>
        public int PendingCount { get; }

        /// <summary>通知メッセージ</summary>
        public string Message { get; }

        public QueueNotification(
            QueueNotificationType type,
            int pendingCount,
            string requestId = null,
            string endpoint = null,
            string message = null)
        {
            Type = type;
            PendingCount = pendingCount;
            RequestId = requestId;
            Endpoint = endpoint;
            Message = message ?? GetDefaultMessage(type);
        }

        private static string GetDefaultMessage(QueueNotificationType type)
        {
            return type switch
            {
                QueueNotificationType.RequestQueued => "リクエストをキューに追加しました",
                QueueNotificationType.RequestCompleted => "リクエストが完了しました",
                QueueNotificationType.RequestRetrying => "リクエストを再試行します",
                QueueNotificationType.RequestFailed => "リクエストが失敗しました",
                QueueNotificationType.ProcessingStarted => "キューの処理を開始しました",
                QueueNotificationType.ProcessingCompleted => "キューの処理が完了しました",
                QueueNotificationType.QueueCleared => "キューをクリアしました",
                _ => "キュー状態が変更されました"
            };
        }
    }

    /// <summary>
    /// キュー状態通知サービスのインターフェース
    /// IRequestQueueのイベントを監視し、ユーザー向け通知を提供
    /// </summary>
    public interface IQueueNotificationService : IDisposable
    {
        /// <summary>
        /// 現在の保留中リクエスト数
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// 失敗したリクエスト数（セッション中）
        /// </summary>
        int FailedCount { get; }

        /// <summary>
        /// 処理中かどうか
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// キュー状態通知イベント
        /// </summary>
        Observable<QueueNotification> OnNotification { get; }

        /// <summary>
        /// 保留中リクエスト数の変更イベント
        /// </summary>
        Observable<int> OnPendingCountChanged { get; }

        /// <summary>
        /// 統計情報をリセット
        /// </summary>
        void ResetStatistics();
    }
}
