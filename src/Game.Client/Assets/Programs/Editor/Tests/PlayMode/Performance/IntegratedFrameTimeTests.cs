using System.Collections;
using System.Collections.Generic;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// L2-6 統合フレームタイム計測。
    /// L2-1 〜 L2-4 の最適化を組み合わせた「全最適化 ON vs 全最適化 OFF」を
    /// 敵 N 体 + 弾 M 発 + ダメージエリア K 個 + 武器検索 1 本 の負荷下で比較する。
    /// Stage 9001 には依存せずテスト独自ミニシーンで行う。
    /// </summary>
    [TestFixture]
    public class IntegratedFrameTimeTests : PlayModeBenchmarkTestBase
    {
        private static readonly int[] EnemyCountScenarios = { 100, 200 };
        private const int ProjectileCount = 50;
        private const int DamageAreaCount = 10;
        private const int Seed = 42;
        private const int MeasureFrames = 500;
        private const int WarmupFrames = 60;

        private const float WeaponRange = 15f;
        private const float AreaRadius = 5f;
        private const float CastRadius = 0.5f;
        private const float CastDistance = 1.0f;
        private const float SpawnHalfExtent = 50f;

        private const float NearDistanceSq = 20f * 20f;
        private const float MidDistanceSq = 40f * 40f;
        private const int NearUpdateInterval = 1;
        private const int MidUpdateInterval = 2;
        private const int FarUpdateInterval = 5;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private LocalPhysicsTestScene _scene;
        private EnemyFactoryForTest.SpawnResult _spawn;
        private Vector3[] _areaPositions;
        private Vector3[] _projectilePositions;
        private Vector3[] _projectileDirections;
        private int[] _lodIntervals;
        private int[] _frameOffsets;
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
        public IEnumerator AllOn_vs_AllOff([ValueSource(nameof(EnemyCountScenarios))] int enemyCount)
        {
            _scene = new LocalPhysicsTestScene($"L2-6_e{enemyCount}");
            _spawn = EnemyFactoryForTest.CreateEnemies(_scene, enemyCount, Seed, SpawnHalfExtent);
            _scene.Simulate(0.02f);
            yield return null;

            _overlapBuffer = new Collider[64];
            _areaPositions = GeneratePositions(DamageAreaCount, Seed + 1);
            GenerateProjectiles(ProjectileCount, Seed + 2);
            ClassifyLod(Vector3.zero);

            // Animator 付与（LOD 更新対象）
            for (int i = 0; i < _spawn.GameObjects.Length; i++)
            {
                if (_spawn.GameObjects[i].GetComponent<Animator>() == null)
                {
                    _spawn.GameObjects[i].AddComponent<Animator>();
                }
            }

            var grid = new SpatialGrid(_spawn.Targets, Mathf.Max(WeaponRange, AreaRadius));

            // --- Warmup OFF ---
            int offFrameCounter = 0;
            int offDistCursor = 0;
            for (int w = 0; w < WarmupFrames; w++)
            {
                RunOff(offFrameCounter++, ref offDistCursor);
                yield return null;
            }

            // --- Measure OFF ---
            int offCounter = 0;
            int offCursor = 0;
            var offMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return offMeasurer.Measure(MeasureFrames, () =>
            {
                RunOff(offCounter++, ref offCursor);
            });
            offMeasurer.CalculateStatistics();

            // --- Warmup ON ---
            int onFrameCounter = 0;
            int onDistCursor = 0;
            int onDistPerFrame = Mathf.Max(1, ProjectileCount / MeasureFrames);
            for (int w = 0; w < WarmupFrames; w++)
            {
                RunOn(onFrameCounter++, ref onDistCursor, onDistPerFrame, grid);
                yield return null;
            }

            // --- Measure ON ---
            int onCounter = 0;
            int onCursor = 0;
            var onMeasurer = new FrameTimeMeasurer(MeasureFrames);
            yield return onMeasurer.Measure(MeasureFrames, () =>
            {
                RunOn(onCounter++, ref onCursor, onDistPerFrame, grid);
            });
            onMeasurer.CalculateStatistics();

            // --- Log ---
            double reduction = offMeasurer.Average > 0
                ? (1.0 - onMeasurer.Average / offMeasurer.Average) * 100.0
                : 0;

            LogBuilder.AppendLine($"[Integrated AllOn vs AllOff enemies={enemyCount}, projectiles={ProjectileCount}, areas={DamageAreaCount}]");
            LogBuilder.AppendLine($"  ALL OFF: avg={offMeasurer.Average:F3}ms / p50={offMeasurer.Median:F3}ms / p95={offMeasurer.P95:F3}ms / p99={offMeasurer.P99:F3}ms / max={offMeasurer.Max:F3}ms");
            LogBuilder.AppendLine($"  ALL ON : avg={onMeasurer.Average:F3}ms / p50={onMeasurer.Median:F3}ms / p95={onMeasurer.P95:F3}ms / p99={onMeasurer.P99:F3}ms / max={onMeasurer.Max:F3}ms");
            LogBuilder.AppendLine($"  Avg reduction: {reduction:F1}%");
            LogBuilder.AppendLine($"  Target 60 FPS = 16.67ms — AllOn avg meets? {(onMeasurer.Average <= 16.67f ? "YES" : "NO")}");
        }

        // ------------------------------------------------------------------
        // OFF: 全て Before 方式
        // ------------------------------------------------------------------

        private void RunOff(int frameCount, ref int distCursor)
        {
            // 武器検索 (L2-3 Before: OverlapSphere + GetComponentInParent)
            int hitCount = _scene.PhysicsScene.OverlapSphere(
                Vector3.zero, WeaponRange, _overlapBuffer, -1, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                _overlapBuffer[i].GetComponentInParent<ICombatTarget>();
            }

            // ダメージエリア (L2-2 Before: 各エリア毎 OverlapSphere)
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                _scene.PhysicsScene.OverlapSphere(_areaPositions[a], AreaRadius, _overlapBuffer, -1, QueryTriggerInteraction.Collide);
            }

            // プロキシ更新 (L2-4 Before: 全 N 体毎フレ Transform + Animator)
            for (int i = 0; i < _spawn.GameObjects.Length; i++)
            {
                WriteProxy(i);
            }

            // プロジェクタイル (L2-1 Concentrated: 1 フレで全 N 発)
            for (int i = 0; i < _projectilePositions.Length; i++)
            {
                _scene.PhysicsScene.SphereCast(
                    _projectilePositions[i], CastRadius, _projectileDirections[i],
                    out _, CastDistance, -1, QueryTriggerInteraction.Collide);
            }
        }

        // ------------------------------------------------------------------
        // ON: 全て After 方式
        // ------------------------------------------------------------------

        private void RunOn(int frameCount, ref int distCursor, int distPerFrame, SpatialGrid grid)
        {
            float weaponRangeSqr = WeaponRange * WeaponRange;
            float areaRangeSqr = AreaRadius * AreaRadius;

            // 武器検索 (L2-3 After: SpatialGrid O(k))
            grid.FindNearest(Vector3.zero, weaponRangeSqr, _spawn.Targets);

            // ダメージエリア (L2-2 After: SpatialGrid 絞込)
            for (int a = 0; a < _areaPositions.Length; a++)
            {
                grid.CountInRange(_areaPositions[a], areaRangeSqr, _spawn.Targets);
            }

            // プロキシ更新 (L2-4 After: LOD 間引き)
            for (int i = 0; i < _spawn.GameObjects.Length; i++)
            {
                int interval = _lodIntervals[i];
                if (interval > 1 && frameCount % interval != _frameOffsets[i] % interval) continue;
                WriteProxy(i);
            }

            // プロジェクタイル (L2-1 Distributed: フレーム分散)
            int n = _projectilePositions.Length;
            for (int k = 0; k < distPerFrame; k++)
            {
                int i = distCursor % n;
                _scene.PhysicsScene.SphereCast(
                    _projectilePositions[i], CastRadius, _projectileDirections[i],
                    out _, CastDistance, -1, QueryTriggerInteraction.Collide);
                distCursor++;
            }
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private void WriteProxy(int i)
        {
            var go = _spawn.GameObjects[i];
            if (go == null) return;
            var t = go.transform;
            var p = t.position;
            t.position = new Vector3(p.x + 0.001f, p.y, p.z);
            var a = go.GetComponent<Animator>();
            if (a != null) a.SetFloat(SpeedHash, 1.0f);
        }

        private void ClassifyLod(Vector3 cameraPos)
        {
            int count = _spawn.GameObjects.Length;
            _lodIntervals = new int[count];
            _frameOffsets = new int[count];
            for (int i = 0; i < count; i++)
            {
                var pos = _spawn.GameObjects[i].transform.position;
                float distSq = (pos - cameraPos).sqrMagnitude;
                if (distSq <= NearDistanceSq) _lodIntervals[i] = NearUpdateInterval;
                else if (distSq <= MidDistanceSq) _lodIntervals[i] = MidUpdateInterval;
                else _lodIntervals[i] = FarUpdateInterval;
                _frameOffsets[i] = i % FarUpdateInterval;
            }
        }

        private static Vector3[] GeneratePositions(int count, int seed)
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

        private class SpatialGrid
        {
            private readonly Dictionary<(int, int), List<int>> _cells = new();
            private readonly float _cellSize;

            public SpatialGrid(EnemyProxyTarget[] registry, float cellSize)
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
                => ((int)Mathf.Floor(pos.x / _cellSize), (int)Mathf.Floor(pos.z / _cellSize));

            public int FindNearest(Vector3 origin, float rangeSqr, EnemyProxyTarget[] registry)
            {
                var centerKey = GetKey(origin);
                int nearest = -1;
                float nearestSqr = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
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
                            nearest = t.NetworkId;
                        }
                    }
                }
                return nearest;
            }

            public int CountInRange(Vector3 origin, float rangeSqr, EnemyProxyTarget[] registry)
            {
                var centerKey = GetKey(origin);
                int total = 0;
                for (int dx = -1; dx <= 1; dx++)
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
                return total;
            }
        }
    }
}
