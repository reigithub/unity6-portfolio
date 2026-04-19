using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-1 プロジェクタイル衝突 集中 vs 分散 対照実験。
    /// 本番 SurvivorProjectile は個別 Update で自発的に SphereCast → 自然に分散している。
    /// テスト内で「人工的集中版」（1 フレで N 発まとめて処理）と「分散版」（毎フレ 1 発のみ）を
    /// 対照実験し、将来まとめ処理設計に変えた場合の p95 frame time spike を定量化する。
    /// </summary>
    [TestFixture]
    public class ProjectileCollisionBurstVsDistributedTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] ProjectileCounts = { 50, 100, 200 };
        private const int EnemyCount = 100;
        private const int Seed = 42;
        private const int MeasureFrames = 300;
        private const int WarmupFrames = 60;
        private const float CastRadius = 0.5f;
        private const float CastDistance = 1.0f;
        private const float SpawnHalfExtent = 50f;

        private LocalPhysicsTestScene _scene;
        private EnemyFactoryForTest.SpawnResult _spawn;
        private Vector3[] _projectilePositions;
        private Vector3[] _projectileDirections;
        private RaycastHit[] _castBuffer;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_spawn.GameObjects != null)
            {
                for (int i = 0; i < _spawn.GameObjects.Length; i++)
                {
                    if (_spawn.GameObjects[i] != null) Object.Destroy(_spawn.GameObjects[i]);
                }
                _spawn = default;
            }
            if (_scene != null)
            {
                yield return _scene.UnloadAsync();
                _scene = null;
            }
        }

        [UnityTest]
        public IEnumerator Concentrated_vs_Distributed_FrameTime(
            [ValueSource(nameof(ProjectileCounts))] int projectileCount)
        {
            _scene = new LocalPhysicsTestScene($"L2-1_p{projectileCount}");
            _spawn = EnemyFactoryForTest.CreateEnemies(_scene, EnemyCount, Seed, SpawnHalfExtent);
            _scene.Simulate(0.02f);
            yield return null;

            GenerateProjectiles(projectileCount, Seed + 1);
            _castBuffer = new RaycastHit[4];

            // --- Warmup: Concentrated ---
            for (int w = 0; w < WarmupFrames; w++)
            {
                ProcessConcentrated();
                yield return null;
            }

            // --- Measure: Concentrated (全 N 発を毎フレ集中処理) ---
            var concMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return concMeasurer.Measure(MeasureFrames, () => ProcessConcentrated());
            concMeasurer.CalculateStatistics();

            // --- Warmup: Distributed ---
            int distCursor = 0;
            int distPerFrame = Mathf.Max(1, projectileCount / MeasureFrames);
            for (int w = 0; w < WarmupFrames; w++)
            {
                ProcessDistributed(ref distCursor, distPerFrame);
                yield return null;
            }

            // --- Measure: Distributed (1 フレあたり projectileCount/MeasureFrames 発のみ処理) ---
            distCursor = 0;
            var distMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return distMeasurer.Measure(MeasureFrames, () =>
            {
                ProcessDistributed(ref distCursor, distPerFrame);
            });
            distMeasurer.CalculateStatistics();

            // --- Log ---
            LogBuilder.AppendLine($"[Projectile Concentrated vs Distributed n={projectileCount}]");
            LogBuilder.AppendLine($"  Concentrated : avg={concMeasurer.Average:F3}ms / p95={concMeasurer.P95:F3}ms / p99={concMeasurer.P99:F3}ms / max={concMeasurer.Max:F3}ms");
            LogBuilder.AppendLine($"  Distributed  : avg={distMeasurer.Average:F3}ms / p95={distMeasurer.P95:F3}ms / p99={distMeasurer.P99:F3}ms / max={distMeasurer.Max:F3}ms");
            double p95Ratio = distMeasurer.P95 > 0 ? concMeasurer.P95 / distMeasurer.P95 : 0;
            LogBuilder.AppendLine($"  Concentrated/Distributed p95 ratio: {p95Ratio:F2}x");
            LogBuilder.AppendLine($"  Distributed per-frame projectiles: {distPerFrame}");
        }

        // ------------------------------------------------------------------
        // Concentrated: 1 フレで N 発全てを SphereCast
        // ------------------------------------------------------------------

        private void ProcessConcentrated()
        {
            var physics = _scene.PhysicsScene;
            for (int i = 0; i < _projectilePositions.Length; i++)
            {
                physics.SphereCast(
                    _projectilePositions[i], CastRadius, _projectileDirections[i],
                    out _, CastDistance, -1, QueryTriggerInteraction.Collide);
            }
        }

        // ------------------------------------------------------------------
        // Distributed: 1 フレあたり perFrame 発のみを処理、cursor で進める
        // ------------------------------------------------------------------

        private void ProcessDistributed(ref int cursor, int perFrame)
        {
            var physics = _scene.PhysicsScene;
            int n = _projectilePositions.Length;
            for (int k = 0; k < perFrame; k++)
            {
                int i = cursor % n;
                physics.SphereCast(
                    _projectilePositions[i], CastRadius, _projectileDirections[i],
                    out _, CastDistance, -1, QueryTriggerInteraction.Collide);
                cursor++;
            }
        }

        // ------------------------------------------------------------------
        // Data generation
        // ------------------------------------------------------------------

        private void GenerateProjectiles(int count, int seed)
        {
            var rng = new System.Random(seed);
            _projectilePositions = new Vector3[count];
            _projectileDirections = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                _projectilePositions[i] = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0.5f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
                float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                _projectileDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
        }
    }
}
