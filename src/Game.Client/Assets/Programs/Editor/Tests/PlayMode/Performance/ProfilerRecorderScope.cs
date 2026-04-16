using System;
using Unity.Profiling;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// ProfilerRecorder の using スコープラッパー。
    /// Main Thread 外のコスト（Animator.Update, PhysicsManager.FixedUpdate 等）を
    /// 測定するため、Stopwatch では捕捉できない処理時間を Profiler 経由で取得する。
    /// </summary>
    public class ProfilerRecorderScope : IDisposable
    {
        private ProfilerRecorder _recorder;

        public ProfilerRecorderScope(ProfilerCategory category, string statName, int capacity = 300)
        {
            _recorder = ProfilerRecorder.StartNew(category, statName, capacity);
        }

        /// <summary>ns 単位の現在値を ms 換算で返す。</summary>
        public double CurrentMs =>
            _recorder.Valid ? _recorder.CurrentValueAsDouble / 1_000_000.0 : 0.0;

        /// <summary>ns 単位の直近サンプル平均を ms 換算で返す。</summary>
        public double AverageMs
        {
            get
            {
                if (!_recorder.Valid || _recorder.Count == 0) return 0.0;
                double sum = 0;
                int count = _recorder.Count;
                for (int i = 0; i < count; i++)
                {
                    sum += _recorder.GetSample(i).Value;
                }
                return (sum / count) / 1_000_000.0;
            }
        }

        public void Dispose()
        {
            if (_recorder.Valid) _recorder.Dispose();
        }
    }
}
