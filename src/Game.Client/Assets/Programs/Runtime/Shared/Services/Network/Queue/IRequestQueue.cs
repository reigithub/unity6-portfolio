using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Game.Shared.Services.Network.Queue
{
    /// <summary>
    /// リクエストキューのインターフェース
    /// オフライン時のリクエストを保存し、オンライン復帰時に再送信
    /// </summary>
    public interface IRequestQueue : IDisposable
    {
        /// <summary>
        /// 保留中のリクエスト数
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// 処理中かどうか
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// リクエストがキューに追加された時のイベント（R3 Observable）
        /// </summary>
        Observable<QueuedRequest> OnRequestQueued { get; }

        /// <summary>
        /// リクエストが完了した時のイベント（R3 Observable）
        /// </summary>
        Observable<QueuedRequest> OnRequestCompleted { get; }

        /// <summary>
        /// リクエストが失敗した時のイベント（R3 Observable）
        /// </summary>
        Observable<QueuedRequest> OnRequestFailed { get; }

        /// <summary>
        /// POSTリクエストをキューに追加
        /// </summary>
        /// <typeparam name="TRequest">リクエストの型</typeparam>
        /// <typeparam name="TResponse">レスポンスの型</typeparam>
        /// <param name="endpoint">エンドポイント</param>
        /// <param name="body">リクエストボディ</param>
        /// <param name="priority">優先度</param>
        /// <param name="maxRetries">最大リトライ回数</param>
        /// <param name="expiration">有効期限</param>
        /// <returns>リクエストID</returns>
        UniTask<string> EnqueuePostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest body,
            RequestPriority priority = RequestPriority.Normal,
            int maxRetries = 3,
            TimeSpan? expiration = null);

        /// <summary>
        /// 指定したリクエストをキャンセル
        /// </summary>
        /// <param name="requestId">リクエストID</param>
        /// <returns>キャンセルに成功した場合はtrue</returns>
        UniTask<bool> CancelAsync(string requestId);

        /// <summary>
        /// キューを処理（オンライン復帰時に呼び出し）
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask ProcessQueueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// キューをクリア
        /// </summary>
        UniTask ClearAsync();

        /// <summary>
        /// 保留中のリクエスト一覧を取得
        /// </summary>
        IReadOnlyList<QueuedRequest> GetPendingRequests();

        /// <summary>
        /// 指定したリクエストを取得
        /// </summary>
        /// <param name="requestId">リクエストID</param>
        QueuedRequest GetRequest(string requestId);
    }
}
