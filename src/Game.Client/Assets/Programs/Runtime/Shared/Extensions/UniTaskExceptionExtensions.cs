using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Services.Network.Policies;
using UnityEngine;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// UniTaskの例外処理・リトライ拡張メソッド
    /// NOTE: .Forget() の例外は UniTaskScheduler.UnobservedTaskException 経由で
    /// GameBootstrap.OnUnobservedTaskException がスタックトレース付きでログ出力する
    /// </summary>
    public static class UniTaskExceptionExtensions
    {
        /// <summary>
        /// 例外発生時にフォールバック処理を実行しながらForgetする
        /// </summary>
        /// <param name="task">対象のUniTask</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <param name="onFallback">フォールバック処理</param>
        public static async void ForgetWithFallback(this UniTask task, string context, Action onFallback)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常系として無視
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{context}] Exception occurred, executing fallback: {ex.Message}");
                onFallback?.Invoke();
            }
        }

        #region Retry Extensions

        /// <summary>
        /// リトライ付きでUniTaskを実行する（指数バックオフ + Jitter）
        /// </summary>
        /// <param name="taskFactory">UniTaskを生成するファクトリ関数</param>
        /// <param name="retryPolicy">リトライポリシー（null時はRetryPolicy.Default）</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <returns>実行結果のUniTask</returns>
        public static async UniTask WithRetry(
            Func<UniTask> taskFactory,
            RetryPolicy retryPolicy = null,
            string context = "AsyncOperation")
        {
            retryPolicy ??= RetryPolicy.Default;
            Exception lastException = null;

            for (var attempt = 0; attempt <= retryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    await taskFactory();
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw; // キャンセルはリトライしない
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < retryPolicy.MaxRetries)
                    {
                        var delayMs = retryPolicy.GetDelayMs(attempt);
                        var jitter = (int)(delayMs * UnityEngine.Random.Range(-0.25f, 0.25f));
                        var actualDelay = Mathf.Max(0, delayMs + jitter);

                        Debug.LogWarning(
                            $"[{context}] Attempt {attempt + 1}/{retryPolicy.MaxRetries + 1} failed: {ex.Message}. " +
                            $"Retrying in {actualDelay}ms...");
                        await UniTask.Delay(actualDelay);
                    }
                }
            }

            Debug.LogError($"[{context}] All {retryPolicy.MaxRetries + 1} attempts failed");
            throw lastException;
        }

        /// <summary>
        /// リトライ付きでUniTaskを実行する（レガシー互換: 固定遅延版）
        /// </summary>
        [Obsolete("Use WithRetry(taskFactory, retryPolicy, context) instead")]
        public static UniTask WithRetry(
            Func<UniTask> taskFactory,
            int maxRetries,
            int retryDelayMs = 500,
            string context = "AsyncOperation")
        {
            var policy = new RetryPolicy
            {
                MaxRetries = maxRetries,
                InitialDelayMs = retryDelayMs,
                BackoffMultiplier = 1.0,
                MaxDelayMs = retryDelayMs,
            };
            return WithRetry(taskFactory, policy, context);
        }

        /// <summary>
        /// リトライ付きでUniTaskを実行する（戻り値あり版、指数バックオフ + Jitter）
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="taskFactory">UniTaskを生成するファクトリ関数</param>
        /// <param name="retryPolicy">リトライポリシー（null時はRetryPolicy.Default）</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <returns>実行結果</returns>
        public static async UniTask<T> WithRetry<T>(
            Func<UniTask<T>> taskFactory,
            RetryPolicy retryPolicy = null,
            string context = "AsyncOperation")
        {
            retryPolicy ??= RetryPolicy.Default;
            Exception lastException = null;

            for (var attempt = 0; attempt <= retryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    return await taskFactory();
                }
                catch (OperationCanceledException)
                {
                    throw; // キャンセルはリトライしない
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < retryPolicy.MaxRetries)
                    {
                        var delayMs = retryPolicy.GetDelayMs(attempt);
                        var jitter = (int)(delayMs * UnityEngine.Random.Range(-0.25f, 0.25f));
                        var actualDelay = Mathf.Max(0, delayMs + jitter);

                        Debug.LogWarning(
                            $"[{context}] Attempt {attempt + 1}/{retryPolicy.MaxRetries + 1} failed: {ex.Message}. " +
                            $"Retrying in {actualDelay}ms...");
                        await UniTask.Delay(actualDelay);
                    }
                }
            }

            Debug.LogError($"[{context}] All {retryPolicy.MaxRetries + 1} attempts failed");
            throw lastException;
        }

        /// <summary>
        /// リトライ付きでUniTaskを実行する（戻り値あり版、レガシー互換: 固定遅延版）
        /// </summary>
        [Obsolete("Use WithRetry<T>(taskFactory, retryPolicy, context) instead")]
        public static UniTask<T> WithRetry<T>(
            Func<UniTask<T>> taskFactory,
            int maxRetries,
            int retryDelayMs = 500,
            string context = "AsyncOperation")
        {
            var policy = new RetryPolicy
            {
                MaxRetries = maxRetries,
                InitialDelayMs = retryDelayMs,
                BackoffMultiplier = 1.0,
                MaxDelayMs = retryDelayMs,
            };
            return WithRetry(taskFactory, policy, context);
        }

        #endregion

        #region Safe Execution Helpers

        /// <summary>
        /// UniTaskVoidを返すメソッドを安全に実行する
        /// 内部で例外をキャッチしてログ出力
        /// </summary>
        /// <param name="asyncAction">実行する非同期アクション</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        public static async void SafeFireAndForget(Func<UniTask> asyncAction, string context)
        {
            try
            {
                await asyncAction();
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常系として無視
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{context}] Unhandled exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// UniTaskVoidを返すメソッドを安全に実行する（フォールバック付き）
        /// </summary>
        /// <param name="asyncAction">実行する非同期アクション</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <param name="onFallback">例外発生時のフォールバック処理</param>
        public static async void SafeFireAndForget(Func<UniTask> asyncAction, string context, Action onFallback)
        {
            try
            {
                await asyncAction();
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常系として無視
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{context}] Exception occurred, executing fallback: {ex.Message}");
                onFallback?.Invoke();
            }
        }

        #endregion
    }
}
