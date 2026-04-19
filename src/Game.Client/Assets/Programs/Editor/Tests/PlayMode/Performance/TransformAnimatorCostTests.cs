using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-5 Transform / Animator 書込単体コスト計測。
    /// ECS 化しても削減できない Unity ランタイムコストの定量化が目的。
    /// Helper 群（PlayModeBenchmarkTestBase / AllocMeasurer / Stopwatch）の健全性検証も兼ねる。
    ///
    /// Animator は AnimatorController 非設定（パラメータ辞書書込 cost のみ測定）。
    /// 実 SurvivorEnemyView も SetFloat(hash, value) 呼出しパターンで同等の cost を持つ。
    /// </summary>
    [TestFixture]
    public class TransformAnimatorCostTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] EntityCounts = { 100, 300, 500 };
        private const int MeasureFrames = 300;
        private const int WarmupFrames = 60;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private GameObject[] _entities;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_entities != null)
            {
                for (int i = 0; i < _entities.Length; i++)
                {
                    if (_entities[i] != null) Object.Destroy(_entities[i]);
                }
                _entities = null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformWrite_PerEntityCost([ValueSource(nameof(EntityCounts))] int n)
        {
            _entities = SpawnTransformOnly(n);
            yield return WarmupFramesIter(WarmupFrames, () => WriteTransform(_entities));

            var sw = new Stopwatch();
            long alloc = AllocMeasurer.Measure(() => { /* reset state */ });
            sw.Restart();
            for (int f = 0; f < MeasureFrames; f++)
            {
                WriteTransform(_entities);
            }
            sw.Stop();

            // 数フレーム挟んで Unity 側処理を走らせる（alloc 測定対象外）
            yield return null;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameUs = totalMs * 1000.0 / MeasureFrames;
            double perEntityUs = perFrameUs / n;

            LogBuilder.AppendLine($"[TransformWrite n={n}]");
            LogBuilder.AppendLine($"  Total    : {totalMs:F2}ms over {MeasureFrames} frames");
            LogBuilder.AppendLine($"  PerFrame : {perFrameUs:F2}us");
            LogBuilder.AppendLine($"  PerEntity: {perEntityUs:F3}us");
            LogBuilder.AppendLine($"  Alloc    : {alloc:N0} bytes (baseline)");
        }

        [UnityTest]
        public IEnumerator AnimatorSetFloat_PerEntityCost([ValueSource(nameof(EntityCounts))] int n)
        {
            _entities = SpawnWithAnimator(n);
            yield return WarmupFramesIter(WarmupFrames, () => WriteAnimator(_entities));

            var sw = new Stopwatch();
            sw.Restart();
            for (int f = 0; f < MeasureFrames; f++)
            {
                WriteAnimator(_entities);
            }
            sw.Stop();
            yield return null;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameUs = totalMs * 1000.0 / MeasureFrames;
            double perEntityUs = perFrameUs / n;

            LogBuilder.AppendLine($"[AnimatorSetFloat n={n}]");
            LogBuilder.AppendLine($"  Total    : {totalMs:F2}ms over {MeasureFrames} frames");
            LogBuilder.AppendLine($"  PerFrame : {perFrameUs:F2}us");
            LogBuilder.AppendLine($"  PerEntity: {perEntityUs:F3}us");
        }

        [UnityTest]
        public IEnumerator TransformAndAnimator_Combined([ValueSource(nameof(EntityCounts))] int n)
        {
            _entities = SpawnWithAnimator(n);
            yield return WarmupFramesIter(WarmupFrames, () =>
            {
                WriteTransform(_entities);
                WriteAnimator(_entities);
            });

            var sw = new Stopwatch();
            sw.Restart();
            for (int f = 0; f < MeasureFrames; f++)
            {
                WriteTransform(_entities);
                WriteAnimator(_entities);
            }
            sw.Stop();
            yield return null;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameUs = totalMs * 1000.0 / MeasureFrames;
            double perEntityUs = perFrameUs / n;

            LogBuilder.AppendLine($"[Combined n={n}]");
            LogBuilder.AppendLine($"  Total    : {totalMs:F2}ms over {MeasureFrames} frames");
            LogBuilder.AppendLine($"  PerFrame : {perFrameUs:F2}us");
            LogBuilder.AppendLine($"  PerEntity: {perEntityUs:F3}us");
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private static GameObject[] SpawnTransformOnly(int count)
        {
            var arr = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = new GameObject($"T_{i}");
                arr[i].transform.position = new Vector3(i % 50, 0, i / 50);
            }
            return arr;
        }

        private static GameObject[] SpawnWithAnimator(int count)
        {
            var arr = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = new GameObject($"A_{i}");
                arr[i].transform.position = new Vector3(i % 50, 0, i / 50);
                arr[i].AddComponent<Animator>();
            }
            return arr;
        }

        private static void WriteTransform(GameObject[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var t = entities[i].transform;
                var p = t.position;
                t.position = new Vector3(p.x + 0.001f, p.y, p.z);
            }
        }

        private static void WriteAnimator(GameObject[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var a = entities[i].GetComponent<Animator>();
                a.SetFloat(SpeedHash, i * 0.01f);
            }
        }

        private static IEnumerator WarmupFramesIter(int frames, System.Action eachFrame)
        {
            for (int f = 0; f < frames; f++)
            {
                eachFrame();
                yield return null;
            }
        }
    }
}
