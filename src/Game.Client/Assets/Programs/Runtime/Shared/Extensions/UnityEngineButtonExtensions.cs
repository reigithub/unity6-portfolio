using System;
using R3;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// UnityEngine.UI.Button用の拡張メソッド
    /// </summary>
    public static class UnityEngineButtonExtensions
    {
        /// <summary>
        /// デフォルトのスロットル間隔（秒）
        /// </summary>
        private const double ThrottleFirstIntervalSeconds = 3D;

        /// <summary>
        /// ボタンクリックをObservableとして取得し、連続クリックを抑制する
        /// 最初のクリック後、指定間隔内のクリックは無視される
        /// </summary>
        /// <param name="button">対象のボタン</param>
        /// <param name="interval">スロットル間隔（秒）。デフォルトは3秒</param>
        /// <returns>クリックイベントのObservable</returns>
        public static Observable<Unit> OnClickAsObservableThrottleFirst(this UnityEngine.UI.Button button, double? interval = 3D)
        {
            return button
                .OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(interval ?? ThrottleFirstIntervalSeconds))
                .AsUnitObservable();
        }
    }
}