using System;
using R3;
using UnityEngine;

namespace Game.Shared.Services.Network.Queue
{
    /// <summary>
    /// キュー状態通知サービスの実装
    /// IRequestQueueのイベントを監視し、UIに通知を提供
    /// </summary>
    public class QueueNotificationService : IQueueNotificationService
    {
        private readonly IRequestQueue _requestQueue;
        private readonly Subject<QueueNotification> _onNotification = new();
        private readonly ReactiveProperty<int> _pendingCount = new(0);
        private readonly CompositeDisposable _disposables = new();
        private int _failedCount;
        private bool _isDisposed;

        public int PendingCount => _requestQueue.PendingCount;
        public int FailedCount => _failedCount;
        public bool IsProcessing => _requestQueue.IsProcessing;
        public Observable<QueueNotification> OnNotification => _onNotification;
        public Observable<int> OnPendingCountChanged => _pendingCount.DistinctUntilChanged();

        public QueueNotificationService(IRequestQueue requestQueue)
        {
            _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));

            // リクエストキューのイベントを購読
            _requestQueue.OnRequestQueued
                .Subscribe(OnRequestQueued)
                .AddTo(_disposables);

            _requestQueue.OnRequestCompleted
                .Subscribe(OnRequestCompleted)
                .AddTo(_disposables);

            _requestQueue.OnRequestFailed
                .Subscribe(OnRequestFailed)
                .AddTo(_disposables);

            // 初期値を設定
            _pendingCount.Value = _requestQueue.PendingCount;
        }

        public void ResetStatistics()
        {
            _failedCount = 0;
            Debug.Log("[QueueNotificationService] Statistics reset");
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _disposables.Dispose();
            _onNotification.Dispose();
            _pendingCount.Dispose();
        }

        private void OnRequestQueued(QueuedRequest request)
        {
            UpdatePendingCount();

            var notification = new QueueNotification(
                QueueNotificationType.RequestQueued,
                PendingCount,
                request.Id,
                request.Endpoint,
                $"リクエストをキューに追加しました: {GetEndpointDisplayName(request.Endpoint)}");

            _onNotification.OnNext(notification);
            Debug.Log($"[QueueNotificationService] Request queued: {request.Endpoint} (Pending: {PendingCount})");
        }

        private void OnRequestCompleted(QueuedRequest request)
        {
            UpdatePendingCount();

            var notification = new QueueNotification(
                QueueNotificationType.RequestCompleted,
                PendingCount,
                request.Id,
                request.Endpoint,
                $"リクエストが完了しました: {GetEndpointDisplayName(request.Endpoint)}");

            _onNotification.OnNext(notification);
            Debug.Log($"[QueueNotificationService] Request completed: {request.Endpoint} (Pending: {PendingCount})");

            // 全リクエスト完了時の通知
            if (PendingCount == 0)
            {
                var completedNotification = new QueueNotification(
                    QueueNotificationType.ProcessingCompleted,
                    0,
                    message: "すべてのリクエストが完了しました");

                _onNotification.OnNext(completedNotification);
            }
        }

        private void OnRequestFailed(QueuedRequest request)
        {
            UpdatePendingCount();

            // リトライ可能かどうかで通知タイプを変える
            var notificationType = request.CanRetry
                ? QueueNotificationType.RequestRetrying
                : QueueNotificationType.RequestFailed;

            if (!request.CanRetry)
            {
                _failedCount++;
            }

            var message = request.CanRetry
                ? $"リクエストを再試行します: {GetEndpointDisplayName(request.Endpoint)} ({request.RetryCount}/{request.MaxRetries})"
                : $"リクエストが失敗しました: {GetEndpointDisplayName(request.Endpoint)}";

            var notification = new QueueNotification(
                notificationType,
                PendingCount,
                request.Id,
                request.Endpoint,
                message);

            _onNotification.OnNext(notification);

            if (request.CanRetry)
            {
                Debug.Log($"[QueueNotificationService] Request retrying: {request.Endpoint} ({request.RetryCount}/{request.MaxRetries})");
            }
            else
            {
                Debug.LogWarning($"[QueueNotificationService] Request failed permanently: {request.Endpoint} - {request.LastErrorMessage}");
            }
        }

        private void UpdatePendingCount()
        {
            _pendingCount.Value = _requestQueue.PendingCount;
        }

        /// <summary>
        /// エンドポイントから表示用の名前を取得
        /// </summary>
        private static string GetEndpointDisplayName(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return "不明";

            // URLの最後の部分を取得
            var lastSlash = endpoint.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < endpoint.Length - 1)
            {
                return endpoint.Substring(lastSlash + 1);
            }

            return endpoint;
        }
    }
}
