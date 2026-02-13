using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using Game.Shared.Services.Network.Models;
using R3;
using UnityEngine;

namespace Game.Shared.Services.Network.Queue
{
    /// <summary>
    /// メモリベースのリクエストキュー実装
    /// アプリ終了時にキューは消失する
    /// </summary>
    public class MemoryRequestQueue : IRequestQueue
    {
        private readonly IApiClient _apiClient;
        private readonly ConcurrentDictionary<string, QueuedRequest> _queue = new();
        private readonly Subject<QueuedRequest> _onRequestQueued = new();
        private readonly Subject<QueuedRequest> _onRequestCompleted = new();
        private readonly Subject<QueuedRequest> _onRequestFailed = new();
        private readonly SemaphoreSlim _processLock = new(1, 1);
        private bool _isProcessing;
        private bool _isDisposed;

        public int PendingCount => _queue.Values.Count(r => r.State == QueuedRequestState.Pending);
        public bool IsProcessing => _isProcessing;

        public Observable<QueuedRequest> OnRequestQueued => _onRequestQueued;
        public Observable<QueuedRequest> OnRequestCompleted => _onRequestCompleted;
        public Observable<QueuedRequest> OnRequestFailed => _onRequestFailed;

        public MemoryRequestQueue(IApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public UniTask<string> EnqueuePostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest body,
            RequestPriority priority = RequestPriority.Normal,
            int maxRetries = 3,
            TimeSpan? expiration = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MemoryRequestQueue));

            var jsonBody = JsonUtility.ToJson(body);
            var responseTypeName = typeof(TResponse).AssemblyQualifiedName;

            var request = new QueuedRequest(
                endpoint,
                "POST",
                jsonBody,
                responseTypeName,
                priority,
                maxRetries,
                expiration);

            _queue.TryAdd(request.Id, request);
            _onRequestQueued.OnNext(request);

            Debug.Log($"[RequestQueue] Enqueued: {request}");

            return UniTask.FromResult(request.Id);
        }

        public UniTask<bool> CancelAsync(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return UniTask.FromResult(false);

            if (_queue.TryGetValue(requestId, out var request))
            {
                if (request.State == QueuedRequestState.Pending)
                {
                    request.State = QueuedRequestState.Cancelled;
                    Debug.Log($"[RequestQueue] Cancelled: {request}");
                    return UniTask.FromResult(true);
                }
            }

            return UniTask.FromResult(false);
        }

        public async UniTask ProcessQueueAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return;

            if (!await _processLock.WaitAsync(0, cancellationToken))
            {
                Debug.Log("[RequestQueue] Already processing, skipping");
                return;
            }

            try
            {
                _isProcessing = true;

                // 優先度順にソートして処理
                var pendingRequests = _queue.Values
                    .Where(r => r.State == QueuedRequestState.Pending && !r.IsExpired)
                    .OrderByDescending(r => r.Priority)
                    .ThenBy(r => r.QueuedAt)
                    .ToList();

                Debug.Log($"[RequestQueue] Processing {pendingRequests.Count} pending requests");

                foreach (var request in pendingRequests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 期限切れチェック
                    if (request.IsExpired)
                    {
                        request.State = QueuedRequestState.Failed;
                        request.LastErrorMessage = "リクエストの有効期限が切れました";
                        _onRequestFailed.OnNext(request);
                        continue;
                    }

                    await ProcessSingleRequestAsync(request, cancellationToken);
                }

                // 完了・失敗・キャンセル済みのリクエストをクリーンアップ
                CleanupCompletedRequests();
            }
            finally
            {
                _isProcessing = false;
                _processLock.Release();
            }
        }

        public UniTask ClearAsync()
        {
            var count = _queue.Count;
            _queue.Clear();
            Debug.Log($"[RequestQueue] Cleared {count} requests");
            return UniTask.CompletedTask;
        }

        public IReadOnlyList<QueuedRequest> GetPendingRequests()
        {
            return _queue.Values
                .Where(r => r.State == QueuedRequestState.Pending)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.QueuedAt)
                .ToList();
        }

        public QueuedRequest GetRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            _queue.TryGetValue(requestId, out var request);
            return request;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _onRequestQueued.Dispose();
            _onRequestCompleted.Dispose();
            _onRequestFailed.Dispose();
            _processLock.Dispose();
            _queue.Clear();
        }

        private async UniTask ProcessSingleRequestAsync(QueuedRequest request, CancellationToken cancellationToken)
        {
            request.State = QueuedRequestState.Processing;

            try
            {
                Debug.Log($"[RequestQueue] Processing: {request}");

                // POSTリクエストを実行
                // 注: 型情報が失われているため、汎用的なobject型で処理
                var response = await ExecutePostRequestAsync(request, cancellationToken);

                if (response.IsSuccess)
                {
                    request.State = QueuedRequestState.Completed;
                    _onRequestCompleted.OnNext(request);
                    Debug.Log($"[RequestQueue] Completed: {request}");
                }
                else
                {
                    HandleRequestFailure(request, response.Error?.Message ?? "Unknown error");
                }
            }
            catch (OperationCanceledException)
            {
                request.State = QueuedRequestState.Pending; // キャンセル時は保留に戻す
                throw;
            }
            catch (Exception ex)
            {
                HandleRequestFailure(request, ex.Message);
            }
        }

        private async UniTask<ApiResponse<object>> ExecutePostRequestAsync(
            QueuedRequest request,
            CancellationToken cancellationToken)
        {
            // JsonUtilityで再度パースしてリクエストを実行
            // 注: 型情報が失われているため、EmptyResponseとして受け取る
            var response = await _apiClient.PostAsync<JsonWrapper, EmptyResponse>(
                request.Endpoint,
                new JsonWrapper { json = request.JsonBody },
                RequestOptions.NoRetry, // キュー内でリトライを管理するためリトライなし
                cancellationToken);

            return new ApiResponse<object>
            {
                IsSuccess = response.IsSuccess,
                Data = response.Data,
                Error = response.Error,
                StatusCode = response.StatusCode
            };
        }

        private void HandleRequestFailure(QueuedRequest request, string errorMessage)
        {
            request.RetryCount++;
            request.LastErrorAt = DateTime.UtcNow;
            request.LastErrorMessage = errorMessage;

            if (request.CanRetry)
            {
                request.State = QueuedRequestState.Pending;
                Debug.Log($"[RequestQueue] Retry scheduled ({request.RetryCount}/{request.MaxRetries}): {request}");
            }
            else
            {
                request.State = QueuedRequestState.Failed;
                _onRequestFailed.OnNext(request);
                Debug.LogWarning($"[RequestQueue] Failed permanently: {request} - {errorMessage}");
            }
        }

        private void CleanupCompletedRequests()
        {
            var toRemove = _queue.Values
                .Where(r => r.State == QueuedRequestState.Completed ||
                           r.State == QueuedRequestState.Failed ||
                           r.State == QueuedRequestState.Cancelled)
                .Select(r => r.Id)
                .ToList();

            foreach (var id in toRemove)
            {
                _queue.TryRemove(id, out _);
            }

            if (toRemove.Count > 0)
            {
                Debug.Log($"[RequestQueue] Cleaned up {toRemove.Count} completed requests");
            }
        }

        /// <summary>
        /// JSONラッパー（JsonUtilityの制限回避用）
        /// </summary>
        [Serializable]
        private class JsonWrapper
        {
            public string json;
        }

        /// <summary>
        /// 空のレスポンス（型が不明な場合用）
        /// </summary>
        [Serializable]
        private class EmptyResponse { }
    }
}
