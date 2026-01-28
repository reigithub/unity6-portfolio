using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// UniTaskの例外処理拡張メソッド
    /// .Forget()呼び出し時の例外ロストを防ぐためのユーティリティ
    /// </summary>
    public static class UniTaskExceptionExtensions
    {
        /// <summary>
        /// 例外をログ出力しながらForgetする
        /// .Forget()の代わりに使用することで、非同期処理の例外を追跡可能にする
        /// </summary>
        /// <param name="task">対象のUniTask</param>
        /// <param name="context">ログに出力するコンテキスト情報（クラス名やメソッド名など）</param>
        public static async void ForgetWithHandler(this UniTask task, string context)
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
                Debug.LogError($"[{context}] Unhandled exception in async operation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 例外をログ出力しながらForgetする（戻り値あり版）
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="task">対象のUniTask</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        public static async void ForgetWithHandler<T>(this UniTask<T> task, string context)
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
                Debug.LogError($"[{context}] Unhandled exception in async operation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 例外をカスタムハンドラーで処理しながらForgetする
        /// </summary>
        /// <param name="task">対象のUniTask</param>
        /// <param name="onException">例外発生時のコールバック</param>
        public static async void ForgetWithHandler(this UniTask task, Action<Exception> onException)
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
                onException?.Invoke(ex);
            }
        }

        /// <summary>
        /// 例外をカスタムハンドラーで処理しながらForgetする（戻り値あり版）
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="task">対象のUniTask</param>
        /// <param name="onException">例外発生時のコールバック</param>
        public static async void ForgetWithHandler<T>(this UniTask<T> task, Action<Exception> onException)
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
                onException?.Invoke(ex);
            }
        }

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
        /// リトライ付きでUniTaskを実行する
        /// </summary>
        /// <param name="taskFactory">UniTaskを生成するファクトリ関数</param>
        /// <param name="maxRetries">最大リトライ回数</param>
        /// <param name="retryDelayMs">リトライ間隔（ミリ秒）</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <returns>実行結果のUniTask</returns>
        public static async UniTask WithRetry(
            Func<UniTask> taskFactory,
            int maxRetries = 3,
            int retryDelayMs = 500,
            string context = "AsyncOperation")
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
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

                    if (attempt < maxRetries)
                    {
                        Debug.LogWarning($"[{context}] Attempt {attempt + 1}/{maxRetries + 1} failed: {ex.Message}. Retrying...");
                        await UniTask.Delay(retryDelayMs);
                    }
                }
            }

            Debug.LogError($"[{context}] All {maxRetries + 1} attempts failed");
            throw lastException;
        }

        /// <summary>
        /// リトライ付きでUniTaskを実行する（戻り値あり版）
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="taskFactory">UniTaskを生成するファクトリ関数</param>
        /// <param name="maxRetries">最大リトライ回数</param>
        /// <param name="retryDelayMs">リトライ間隔（ミリ秒）</param>
        /// <param name="context">ログに出力するコンテキスト情報</param>
        /// <returns>実行結果</returns>
        public static async UniTask<T> WithRetry<T>(
            Func<UniTask<T>> taskFactory,
            int maxRetries = 3,
            int retryDelayMs = 500,
            string context = "AsyncOperation")
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
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

                    if (attempt < maxRetries)
                    {
                        Debug.LogWarning($"[{context}] Attempt {attempt + 1}/{maxRetries + 1} failed: {ex.Message}. Retrying...");
                        await UniTask.Delay(retryDelayMs);
                    }
                }
            }

            Debug.LogError($"[{context}] All {maxRetries + 1} attempts failed");
            throw lastException;
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
