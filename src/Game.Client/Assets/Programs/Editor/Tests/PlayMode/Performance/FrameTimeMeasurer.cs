using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// PlayMode 向けフレーム時間計測。
    /// 各フレームの onEachFrame 実行時間を ms 単位で記録し、Finalize() で in-place sort + Percentile 一括計算。
    /// 計測中のアロケーションを避けるため List は事前容量確保、Percentile は Finalize 後の fold 済み値を返す。
    /// </summary>
    public class FrameTimeMeasurer
    {
        private readonly List<float> _frameTimes;
        private bool _finalized;

        public FrameTimeMeasurer(int capacity = 2000)
        {
            _frameTimes = new List<float>(capacity);
        }

        public float Average { get; private set; }
        public float Median { get; private set; }
        public float P95 { get; private set; }
        public float P99 { get; private set; }
        public float Max { get; private set; }
        public int SampleCount => _frameTimes.Count;

        /// <summary>
        /// frameCount フレーム計測する。各フレームの先頭で onEachFrame を実行し、
        /// 実行完了後 yield return null で次フレームへ進む。計測は実行時間のみ。
        /// </summary>
        public IEnumerator Measure(int frameCount, Action onEachFrame = null)
        {
            _frameTimes.Clear();
            _finalized = false;

            var sw = new Stopwatch();
            for (int i = 0; i < frameCount; i++)
            {
                sw.Restart();
                onEachFrame?.Invoke();
                sw.Stop();
                _frameTimes.Add((float)sw.Elapsed.TotalMilliseconds);
                yield return null;
            }
        }

        /// <summary>
        /// 計測完了後 1 度だけ呼び出す。以降の percentile getter は fold 済み値を返す。
        /// </summary>
        public void CalculateStatistics()
        {
            if (_finalized) return;
            if (_frameTimes.Count == 0)
            {
                Average = Median = P95 = P99 = Max = 0f;
                _finalized = true;
                return;
            }

            _frameTimes.Sort();

            double sum = 0;
            for (int i = 0; i < _frameTimes.Count; i++) sum += _frameTimes[i];
            Average = (float)(sum / _frameTimes.Count);

            Median = GetPercentileSorted(50);
            P95 = GetPercentileSorted(95);
            P99 = GetPercentileSorted(99);
            Max = _frameTimes[_frameTimes.Count - 1];
            _finalized = true;
        }

        private float GetPercentileSorted(float pct)
        {
            int idx = Mathf.Clamp((int)(_frameTimes.Count * pct / 100f), 0, _frameTimes.Count - 1);
            return _frameTimes[idx];
        }
    }
}
