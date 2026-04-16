using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-2 継続ダメージエリアの毎フレ範囲検査比較。
    /// SurvivorGroundDamageArea は毎フレ全エリアで OverlapSphere を呼び、範囲内敵に OnHit 発火する。
    /// エリア N 個 × 敵 100 体で、直接 OverlapSphere vs 共有 Spatial Grid 候補絞込の累積コストを比較する。
    ///
    /// L2-3 と軸を分けるため「継続範囲」特性に特化（複数エリア × 60 フレーム累積コスト）。
    /// </summary>
    [TestFixture]
    public class DamageAreaContinuousRangeTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] AreaCounts = { 10, 30, 50 };
        private const int EnemyCount = 100;
        private const int Seed = 42;
        private const int ContinuousFrames = 60;
        private const int WarmupFrames = 30;
        private const float AreaRadius = 5f;
        private const float SpawnHalfExtent = 50f;

        private LocalPhysicsTestScene _scene;
        private EnemyFactoryForTest.SpawnResult _spawn;
        private Vector3[] _areaPositions;
        private Collider[] _overlapBuffer;

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
        public IEnumerator OverlapSphere_vs_SpatialGrid(
            [ValueSource(nameof(AreaCounts))] int areaCount)
        {
            _scene = new LocalPhysicsTestScene($"L2-2_a{areaCount}");
            _spawn = EnemyFactoryForTest.CreateEnemies(_scene, EnemyCount, Seed, SpawnHalfExtent);
            _overlapBuffer = new Collider[64];

            _scene.Simulate(0.02f);
            yield return null;

            _areaPositions = GenerateAreaPositions(areaCount, Seed + 1);

            // SpatialGrid 事前構築
            var grid = new SpatialGrid(_spawn.Targets, AreaRadius);

            // --- 結果一致率検証（1 フレ分）---
            var overlapHits = new HashSet<int>();
            var gridHits = new HashSet<int>();
            CollectOverlap(overlapHits, _scene.PhysicsScene, _overlapBuffer);
            CollectGrid(gridHits, grid, _spawn.Targets);
            CollectionAssert.AreEquivalent(overlapHits, gridHits,
                $"OverlapSphere vs Grid の hit 集合不一致: areaCount={areaCount}");

            // --- Warmup ---
            for (int w = 0; w < WarmupFrames; w++)
            {
                CollectOverlap(overlapHits, _scene.PhysicsScene, _overlapBuffer);
                CollectGrid(gridHits, grid, _spawn.Targets);
                yield return null;
            }

            // --- Measure: OverlapSphere 直接 ---
            var sw = new Stopwatch();
            sw.Restart();
            int overlapHitTotal = 0;
            for (int f = 0; f < ContinuousFrames; f++)
            {
                overlapHitTotal += CountOverlap(_scene.PhysicsScene, _overlapBuffer);
            }
            sw.Stop();
            double overlapMs = sw.Elapsed.TotalMilliseconds;

            // --- Measure: SpatialGrid ---
            sw.Restart();
            int gridHitTotal = 0;
            for (int f = 0; f < ContinuousFrames; f++)
            {
                gridHitTotal += CountGrid(grid, _spawn.Targets);
            }
            sw.Stop();
            double gridMs = sw.Elapsed.TotalMilliseconds;

            // --- Log ---
            double perOverlapFrameMs = overlapMs / ContinuousFrames;
            double perGridFrameMs = gridMs / ContinuousFrames;
            double speedup = gridMs > 0 ? overlapMs / gridMs : 0;

            LogBuilder.AppendLine($"[DamageArea ContinuousRange areaCount={areaCount}, enemies={EnemyCount}]");
            LogBuilder.AppendLine($"  OverlapSphere : {overlapMs:F2}ms total / {perOverlapFrameMs:F3}ms per frame / hits={overlapHitTotal}");
            LogBuilder.AppendLine($"  SpatialGrid   : {gridMs:F2}ms total / {perGridFrameMs:F3}ms per frame / hits={gridHitTotal}");
            LogBuilder.AppendLine($"  Speedup       : {speedup:F2}x");
        }

        // ------------------------------------------------------------------
        // OverlapSphere 直接（Before）
        // ------------------------------------------------------------------

        private void CollectOverlap(HashSet<int> hits, PhysicsScene scene, Collider[] buffer)
        {
            hits.Clear();
            float rangeSqr = AreaRadius * AreaRadius;
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                int c = scene.OverlapSphere(_areaPositions[a], AreaRadius, buffer, -1, QueryTriggerInteraction.Collide);
                for (int i = 0; i < c; i++)
                {
                    var proxy = buffer[i].GetComponent<Game.MVP.Survivor.Enemy.EnemyProxyTarget>();
                    if (proxy == null) continue;
                    // OverlapSphere は SphereCollider.radius 分膨張して拾うため、
                    // Grid 側と同じ CenterPosition 基準の sqrMagnitude でフィルタを揃える
                    float sqr = (_areaPositions[a] - proxy.CenterPosition).sqrMagnitude;
                    if (sqr <= rangeSqr) hits.Add(proxy.NetworkId);
                }
            }
        }

        private int CountOverlap(PhysicsScene scene, Collider[] buffer)
        {
            int total = 0;
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                total += scene.OverlapSphere(_areaPositions[a], AreaRadius, buffer, -1, QueryTriggerInteraction.Collide);
            }
            return total;
        }

        // ------------------------------------------------------------------
        // SpatialGrid 候補絞込（After）
        // ------------------------------------------------------------------

        private void CollectGrid(HashSet<int> hits, SpatialGrid grid, Game.MVP.Survivor.Enemy.EnemyProxyTarget[] registry)
        {
            hits.Clear();
            float rangeSqr = AreaRadius * AreaRadius;
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                grid.CollectInRange(_areaPositions[a], rangeSqr, registry, hits);
            }
        }

        private int CountGrid(SpatialGrid grid, Game.MVP.Survivor.Enemy.EnemyProxyTarget[] registry)
        {
            float rangeSqr = AreaRadius * AreaRadius;
            int total = 0;
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                total += grid.CountInRange(_areaPositions[a], rangeSqr, registry);
            }
            return total;
        }

        private static Vector3[] GenerateAreaPositions(int count, int seed)
        {
            var rng = new System.Random(seed);
            var arr = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
            }
            return arr;
        }

        private class SpatialGrid
        {
            private readonly Dictionary<(int, int), List<int>> _cells = new();
            private readonly float _cellSize;

            public SpatialGrid(Game.MVP.Survivor.Enemy.EnemyProxyTarget[] registry, float cellSize)
            {
                _cellSize = cellSize;
                for (int i = 0; i < registry.Length; i++)
                {
                    var t = registry[i];
                    if (t == null) continue;
                    var key = GetKey(t.CenterPosition);
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

            public void CollectInRange(Vector3 origin, float rangeSqr,
                Game.MVP.Survivor.Enemy.EnemyProxyTarget[] registry, HashSet<int> hits)
            {
                var centerKey = GetKey(origin);
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
                            if (t == null) continue;
                            float sqr = (origin - t.CenterPosition).sqrMagnitude;
                            if (sqr <= rangeSqr)
                            {
                                hits.Add(t.NetworkId);
                            }
                        }
                    }
                }
            }

            public int CountInRange(Vector3 origin, float rangeSqr,
                Game.MVP.Survivor.Enemy.EnemyProxyTarget[] registry)
            {
                var centerKey = GetKey(origin);
                int total = 0;
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
                            if (t == null) continue;
                            float sqr = (origin - t.CenterPosition).sqrMagnitude;
                            if (sqr <= rangeSqr) total++;
                        }
                    }
                }
                return total;
            }
        }
    }
}
