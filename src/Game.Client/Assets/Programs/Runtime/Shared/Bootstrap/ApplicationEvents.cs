using System;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// アプリケーションレベルのイベント
    /// 下位アセンブリから上位アセンブリの機能を呼び出すための仕組み
    /// </summary>
    public static class ApplicationEvents
    {
        /// <summary>
        /// アプリケーション終了リクエスト
        /// Game.Appで購読し、GameBootstrap.Shutdownを実行する
        /// </summary>
        public static Func<UniTask> OnShutdownRequested;

        /// <summary>
        /// タイトル画面に戻るリクエスト
        /// Game.Appで購読し、GameBootstrap.ReturnToTitleAsyncを実行する
        /// </summary>
        public static Func<UniTask> OnReturnToTitleRequested;

        /// <summary>
        /// アプリケーション終了をリクエストする
        /// </summary>
        public static void RequestShutdown()
        {
            OnShutdownRequested?.Invoke();
        }

        /// <summary>
        /// タイトル画面に戻ることをリクエストする
        /// </summary>
        public static UniTask RequestReturnToTitleAsync()
        {
            return OnReturnToTitleRequested?.Invoke() ?? UniTask.CompletedTask;
        }
    }
}