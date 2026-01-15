using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// アプリケーションレベルのイベント
    /// 下位アセンブリから上位アセンブリの機能を呼び出すための仕組み
    /// </summary>
    public static class ApplicationEvents
    {
        #region TimeScale

        /// <summary>
        /// ゲーム時間を一時停止
        /// </summary>
        public static void PauseTime() => Time.timeScale = 0f;

        /// <summary>
        /// ゲーム時間を再開
        /// </summary>
        public static void ResumeTime() => Time.timeScale = 1f;

        /// <summary>
        /// TimeScaleを任意の値に設定
        /// </summary>
        public static void SetTimeScale(float scale) => Time.timeScale = scale;

        #endregion

        #region Cursor

        /// <summary>
        /// カーソルを表示（ロック解除）
        /// </summary>
        public static void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// カーソルを非表示（中央にロック）
        /// </summary>
        public static void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// カーソルの表示状態を設定
        /// </summary>
        /// <param name="visible">true: 表示＆ロック解除, false: 非表示＆ロック</param>
        public static void SetCursorVisible(bool visible)
        {
            if (visible)
                ShowCursor();
            else
                HideCursor();
        }

        #endregion

        #region Application Lifecycle

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

        #endregion
    }
}