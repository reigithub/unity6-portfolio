using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-3 最近傍敵探索 End-to-End 比較。
    /// SurvivorAutoFireWeapon.FindNearestEnemy の本番実装パターンを PlayMode で再現し、
    /// 3 方式（OverlapSphere+GetComponentInParent / Registry 線形 / SpatialGrid）を計測する。
    ///
    /// 実 EnemyProxyTarget + MockEnemyDeathQuery（Dictionary lookup コスト再現）を使用し、
    /// 測定値が本番より軽く見えるバイアスを排除する。
    /// </summary>
    [TestFixture]
    public class WeaponTargetingEndToEndTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] EnemyCounts = { 100, 500, 1000, 2000 };
        private const int Seed = 42;
        private const int WarmupIterations = 200;
        private const int MeasureIterations = 1000;
        private const float SearchRange = 15f;
        private const float SpawnHalfExtent = 50f;

        private LocalPhysicsTestScene _scene;
        private EnemyFactoryForTest.SpawnResult _spawn;
        private Collider[] _hitBuffer;

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
        public IEnumerator FindNearest_OverlapSphere_vs_Registry_vs_Grid(
            [ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            _scene = new LocalPhysicsTestScene($"L2-3_n{enemyCount}");
            _spawn = EnemyFactoryForTest.CreateEnemies(_scene, enemyCount, Seed, SpawnHalfExtent);
            _hitBuffer = new Collider[enemyCount];

            // Physics Scene に Collider を同期
            _scene.Simulate(0.02f);
            yield return null;

            var origin = new Vector3(0f, 0f, 0f);
            float rangeSqr = SearchRange * SearchRange;

            // Registry: 全 EnemyProxyTarget の配列
            var registry = _spawn.Targets;

            // SpatialGrid 事前構築
            var grid = new SpatialGrid(registry, SearchRange);

            // --- 結果一致率検証 ---
            int overlapNearest = FindNearestOverlapSphere(origin, _scene.PhysicsScene, _hitBuffer);
            int registryNearest = FindNearestRegistry(origin, registry, rangeSqr);
            int gridNearest = grid.FindNearest(origin, rangeSqr, registry);

            Assert.AreEqual(overlapNearest, registryNearest,
                $"OverlapSphere vs Registry の nearest が不一致: n={enemyCount}");
            Assert.AreEqual(overlapNearest, gridNearest,
                $"OverlapSphere vs Grid の nearest が不一致: n={enemyCount}");

            // --- Warmup ---
            for (int i = 0; i < WarmupIterations; i++)
            {
                FindNearestOverlapSphere(origin, _scene.PhysicsScene, _hitBuffer);
            }
            for (int i = 0; i < WarmupIterations; i++)
            {
                FindNearestRegistry(origin, registry, rangeSqr);
            }
            for (int i = 0; i < WarmupIterations; i++)
            {
                grid.FindNearest(origin, rangeSqr, registry);
            }

            // --- Measure: OverlapSphere + GetComponentInParent ---
            long overlapAlloc = 0;
            var sw = new Stopwatch();
            overlapAlloc = AllocMeasurer.Measure(() =>
            {
                sw.Restart();
                for (int i = 0; i < MeasureIterations; i++)
                {
                    FindNearestOverlapSphere(origin, _scene.PhysicsScene, _hitBuffer);
                }
                sw.Stop();
            });
            double overlapMs = sw.Elapsed.TotalMilliseconds;

            // --- Measure: Registry 線形 ---
            long registryAlloc;
            registryAlloc = AllocMeasurer.Measure(() =>
            {
                sw.Restart();
                for (int i = 0; i < MeasureIterations; i++)
                {
                    FindNearestRegistry(origin, registry, rangeSqr);
                }
                sw.Stop();
            });
            double registryMs = sw.Elapsed.TotalMilliseconds;

            // --- Measure: SpatialGrid ---
            long gridAlloc;
            gridAlloc = AllocMeasurer.Measure(() =>
            {
                sw.Restart();
                for (int i = 0; i < MeasureIterations; i++)
                {
                    grid.FindNearest(origin, rangeSqr, registry);
                }
                sw.Stop();
            });
            double gridMs = sw.Elapsed.TotalMilliseconds;

            // --- Log ---
            double perOverlapUs = overlapMs * 1000.0 / MeasureIterations;
            double perRegistryUs = registryMs * 1000.0 / MeasureIterations;
            double perGridUs = gridMs * 1000.0 / MeasureIterations;
            double registrySpeedup = registryMs > 0 ? overlapMs / registryMs : 0;
            double gridSpeedup = gridMs > 0 ? overlapMs / gridMs : 0;

            LogBuilder.AppendLine($"[FindNearest EndToEnd n={enemyCount}]");
            LogBuilder.AppendLine($"  OverlapSphere+GetComp : {overlapMs:F2}ms / {perOverlapUs:F2}us per call / {overlapAlloc:N0} bytes alloc");
            LogBuilder.AppendLine($"  Registry linear       : {registryMs:F2}ms / {perRegistryUs:F2}us per call / {registryAlloc:N0} bytes alloc");
            LogBuilder.AppendLine($"  SpatialGrid           : {gridMs:F2}ms / {perGridUs:F2}us per call / {gridAlloc:N0} bytes alloc");
            LogBuilder.AppendLine($"  Registry speedup : {registrySpeedup:F2}x");
            LogBuilder.AppendLine($"  Grid speedup     : {gridSpeedup:F2}x");
        }

        // ------------------------------------------------------------------
        // Before: 本番 SurvivorAutoFireWeapon.FindNearestEnemy 相当
        // ------------------------------------------------------------------

        private static int FindNearestOverlapSphere(
            Vector3 origin, PhysicsScene physicsScene, Collider[] hitBuffer)
        {
            int hitCount = physicsScene.OverlapSphere(
                origin, SearchRange, hitBuffer, -1, QueryTriggerInteraction.Collide);

            int nearestNetworkId = -1;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var target = hitBuffer[i].GetComponentInParent<ICombatTarget>();
                if (target == null || target.IsDead) continue;
                float sqr = (origin - target.CenterPosition).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    if (target is EnemyProxyTarget proxy) nearestNetworkId = proxy.NetworkId;
                }
            }
            return nearestNetworkId;
        }

        // ------------------------------------------------------------------
        // After-1: 事前索引した EnemyProxyTarget 配列を線形走査
        // ------------------------------------------------------------------

        private static int FindNearestRegistry(
            Vector3 origin, EnemyProxyTarget[] registry, float rangeSqr)
        {
            int nearestNetworkId = -1;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < registry.Length; i++)
            {
                var t = registry[i];
                if (t == null || t.IsDead) continue;
                float sqr = (origin - t.CenterPosition).sqrMagnitude;
                if (sqr <= rangeSqr && sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearestNetworkId = t.NetworkId;
                }
            }
            return nearestNetworkId;
        }

        // ------------------------------------------------------------------
        // After-2: SpatialGrid による O(k) 走査
        // ------------------------------------------------------------------

        private class SpatialGrid
        {
            private readonly Dictionary<(int, int), List<int>> _cells
                = new Dictionary<(int, int), List<int>>();
            private readonly float _cellSize;

            public SpatialGrid(EnemyProxyTarget[] registry, float cellSize)
            {
                _cellSize = cellSize;
                for (int i = 0; i < registry.Length; i++)
                {
                    var t = registry[i];
                    if (t == null) continue;
                    var pos = t.CenterPosition;
                    var key = GetKey(pos);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        _cells[key] = list;
                    }
                    list.Add(i);
                }
            }

            private (int, int) GetKey(Vector3 pos)
            {
                return ((int)Mathf.Floor(pos.x / _cellSize), (int)Mathf.Floor(pos.z / _cellSize));
            }

            public int FindNearest(Vector3 origin, float rangeSqr, EnemyProxyTarget[] registry)
            {
                var centerKey = GetKey(origin);
                int nearestNetworkId = -1;
                float nearestSqr = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var key = (centerKey.Item1 + dx, centerKey.Item2 + dz);
                        if (!_cells.TryGetValue(key, out var list)) continue;
                        for (int k = 0; k < list.Count; k++)
                        {
                            int idx = list[k];
                            var t = registry[idx];
                            if (t == null || t.IsDead) continue;
                            float sqr = (origin - t.CenterPosition).sqrMagnitude;
                            if (sqr <= rangeSqr && sqr < nearestSqr)
                            {
                                nearestSqr = sqr;
                                nearestNetworkId = t.NetworkId;
                            }
                        }
                    }
                }
                return nearestNetworkId;
            }
        }
    }
}
