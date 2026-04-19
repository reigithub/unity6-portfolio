using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-4 LOD 間引きの実効値計測。
    /// SurvivorEnemyView の LOD 分類（Near=毎フレ / Mid=2フレ / Far=5フレ）を
    /// テスト内で MockLodUpdater として再現し、LOD ON / OFF の frame time を比較する。
    ///
    /// 本番コードは無改変。比較は Before=LOD OFF（全プロキシ毎フレ更新）、After=LOD ON。
    /// </summary>
    [TestFixture]
    public class LodEffectivenessTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] EntityCounts = { 100, 300, 500 };
        private const int MeasureFrames = 300;
        private const int WarmupFrames = 60;

        private const float NearDistanceSq = 20f * 20f;
        private const float MidDistanceSq = 40f * 40f;
        private const int NearUpdateInterval = 1;
        private const int MidUpdateInterval = 2;
        private const int FarUpdateInterval = 5;
        private const float SpawnHalfExtent = 50f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private GameObject[] _entities;
        private Vector3[] _positions;
        private int[] _lodIntervals;
        private int[] _frameOffsets;

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
        public IEnumerator LodOn_vs_LodOff_FrameTime([ValueSource(nameof(EntityCounts))] int n)
        {
            Spawn(n);

            // カメラ位置（原点）から各プロキシの LOD を分類
            var cameraPos = Vector3.zero;
            ClassifyAllLod(cameraPos);

            // --- Warmup (LOD OFF) ---
            int frameCounter = 0;
            for (int f = 0; f < WarmupFrames; f++)
            {
                UpdateAllProxies(Time.deltaTime);
                frameCounter++;
                yield return null;
            }

            // --- Measure (LOD OFF) ---
            var offMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return offMeasurer.Measure(MeasureFrames, () =>
            {
                UpdateAllProxies(Time.deltaTime);
            });
            offMeasurer.CalculateStatistics();

            // --- Warmup (LOD ON) ---
            frameCounter = 0;
            for (int f = 0; f < WarmupFrames; f++)
            {
                UpdateWithLod(frameCounter, Time.deltaTime);
                frameCounter++;
                yield return null;
            }

            // --- Measure (LOD ON) ---
            int measureCounter = 0;
            var onMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return onMeasurer.Measure(MeasureFrames, () =>
            {
                UpdateWithLod(measureCounter++, Time.deltaTime);
            });
            onMeasurer.CalculateStatistics();

            // --- Log ---
            double reduction = offMeasurer.Average > 0
                ? (1.0 - onMeasurer.Average / offMeasurer.Average) * 100.0
                : 0;

            LogBuilder.AppendLine($"[LOD FrameTime n={n}]");
            LogBuilder.AppendLine($"  LOD OFF: avg={offMeasurer.Average:F3}ms / p95={offMeasurer.P95:F3}ms / p99={offMeasurer.P99:F3}ms / max={offMeasurer.Max:F3}ms");
            LogBuilder.AppendLine($"  LOD ON : avg={onMeasurer.Average:F3}ms / p95={onMeasurer.P95:F3}ms / p99={onMeasurer.P99:F3}ms / max={onMeasurer.Max:F3}ms");
            LogBuilder.AppendLine($"  Reduction (avg): {reduction:F1}%");

            int nearCount = 0, midCount = 0, farCount = 0;
            for (int i = 0; i < _lodIntervals.Length; i++)
            {
                if (_lodIntervals[i] == NearUpdateInterval) nearCount++;
                else if (_lodIntervals[i] == MidUpdateInterval) midCount++;
                else farCount++;
            }
            LogBuilder.AppendLine($"  LOD distribution: Near={nearCount} Mid={midCount} Far={farCount}");
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private void Spawn(int count)
        {
            var rng = new System.Random(42);
            _entities = new GameObject[count];
            _positions = new Vector3[count];
            _lodIntervals = new int[count];
            _frameOffsets = new int[count];

            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);

                _entities[i] = new GameObject($"Proxy_{i}");
                _entities[i].transform.position = pos;
                _entities[i].AddComponent<Animator>();

                _positions[i] = pos;
                _frameOffsets[i] = i % FarUpdateInterval;
            }
        }

        private void ClassifyAllLod(Vector3 cameraPos)
        {
            for (int i = 0; i < _positions.Length; i++)
            {
                float distSq = (_positions[i] - cameraPos).sqrMagnitude;
                if (distSq <= NearDistanceSq) _lodIntervals[i] = NearUpdateInterval;
                else if (distSq <= MidDistanceSq) _lodIntervals[i] = MidUpdateInterval;
                else _lodIntervals[i] = FarUpdateInterval;
            }
        }

        private void UpdateAllProxies(float dt)
        {
            for (int i = 0; i < _entities.Length; i++)
            {
                WriteTransformAndAnimator(i, dt);
            }
        }

        private void UpdateWithLod(int frameCount, float dt)
        {
            for (int i = 0; i < _entities.Length; i++)
            {
                int interval = _lodIntervals[i];
                if (interval > 1 && frameCount % interval != _frameOffsets[i] % interval)
                {
                    continue;
                }
                WriteTransformAndAnimator(i, dt);
            }
        }

        private void WriteTransformAndAnimator(int i, float dt)
        {
            var t = _entities[i].transform;
            var p = t.position;
            t.position = new Vector3(p.x + dt * 0.1f, p.y, p.z);
            var a = _entities[i].GetComponent<Animator>();
            if (a != null) a.SetFloat(SpeedHash, 1.0f);
        }
    }
}
