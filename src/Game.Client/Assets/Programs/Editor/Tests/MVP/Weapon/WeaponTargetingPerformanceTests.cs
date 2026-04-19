using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.MVP.Weapon
{
    /// <summary>
    /// L1-2 最近傍敵探索の Layer 1 (EditMode) パフォーマンスベンチ。
    /// SurvivorAutoFireWeapon.FindNearestEnemy の最終 sqrMagnitude 比較部分を抽出し、
    /// Before: O(N) 線形走査 vs After: O(k) pure C# Dictionary grid を比較する。
    ///
    /// Layer 1 の位置付け: 計算量削減の純粋アルゴリズム比較。
    /// Burst / Physics / GameObject 依存なし。
    /// </summary>
    [TestFixture]
    public class WeaponTargetingPerformanceTests
    {
        private const int Seed = 42;
        private static readonly int[] EnemyCounts = { 100, 500, 1000, 2000, 5000 };
        private const int WarmupIterations = 100;
        private const int MeasureIterations = 1000;
        private const float SearchRange = 15f;
        private const float SpawnHalfExtent = 50f;

        private StringBuilder _logBuilder;
        private string _logFilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir,
                $"WeaponTargetingPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        [SetUp]
        public void SetUp()
        {
            _logBuilder = new StringBuilder();
        }

        [TearDown]
        public void TearDown()
        {
            if (_logBuilder != null && _logBuilder.Length > 0)
            {
                var logContent = _logBuilder.ToString();
                Debug.Log(logContent);
                File.AppendAllText(_logFilePath, logContent + "\n");
            }
        }

        [Test]
        public void FindNearest_LinearVsSpatialGrid([ValueSource(nameof(EnemyCounts))] int enemyCount)
        {
            var (positions, isDead) = GenerateEnemies(enemyCount, Seed);
            var origin = new Vector3(0f, 0f, 0f);
            float rangeSqr = SearchRange * SearchRange;

            // 結果一致率検証: Before/After が同じ最近傍を返すこと
            var grid = new SpatialGrid(positions, isDead, SearchRange);
            int linearResult = FindNearestLinear(origin, positions, isDead, rangeSqr);
            int gridResult = grid.FindNearest(origin, rangeSqr, positions, isDead);
            Assert.AreEqual(linearResult, gridResult,
                $"Result mismatch: n={enemyCount}, linear={linearResult}, grid={gridResult}");

            // --- Warmup (Linear) ---
            for (int i = 0; i < WarmupIterations; i++)
            {
                FindNearestLinear(origin, positions, isDead, rangeSqr);
            }

            // --- Measure (Linear) ---
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                FindNearestLinear(origin, positions, isDead, rangeSqr);
            }
            sw.Stop();
            var linearMs = sw.Elapsed.TotalMilliseconds;
            var linearAlloc = GC.GetTotalMemory(false) - memBefore;

            // --- Warmup (Grid) ---
            // Note: Grid は事前構築のみ、探索時は構築済み grid を再利用
            for (int i = 0; i < WarmupIterations; i++)
            {
                grid.FindNearest(origin, rangeSqr, positions, isDead);
            }

            // --- Measure (Grid) ---
            GC.Collect();
            GC.WaitForPendingFinalizers();
            memBefore = GC.GetTotalMemory(true);
            sw.Restart();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                grid.FindNearest(origin, rangeSqr, positions, isDead);
            }
            sw.Stop();
            var gridMs = sw.Elapsed.TotalMilliseconds;
            var gridAlloc = GC.GetTotalMemory(false) - memBefore;

            // --- Log ---
            double perLinearUs = linearMs * 1000.0 / MeasureIterations;
            double perGridUs = gridMs * 1000.0 / MeasureIterations;
            double speedup = gridMs > 0 ? linearMs / gridMs : 0;

            _logBuilder.AppendLine($"[FindNearest LinearVsSpatialGrid] n={enemyCount}");
            _logBuilder.AppendLine($"  Linear : {linearMs:F2}ms total / {perLinearUs:F2}us per call / {linearAlloc:N0} bytes alloc");
            _logBuilder.AppendLine($"  Grid   : {gridMs:F2}ms total / {perGridUs:F2}us per call / {gridAlloc:N0} bytes alloc");
            _logBuilder.AppendLine($"  Speedup: {speedup:F2}x");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static (Vector3[] positions, bool[] isDead) GenerateEnemies(int count, int seed)
        {
            var rng = new System.Random(seed);
            var positions = new Vector3[count];
            var isDead = new bool[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
                isDead[i] = false;
            }
            return (positions, isDead);
        }

        /// <summary>
        /// Before 版: 全敵を線形走査 O(N)
        /// </summary>
        private static int FindNearestLinear(
            Vector3 origin, Vector3[] positions, bool[] isDead, float rangeSqr)
        {
            int nearest = -1;
            float minSqr = float.MaxValue;
            for (int i = 0; i < positions.Length; i++)
            {
                if (isDead[i]) continue;
                float sqr = (origin - positions[i]).sqrMagnitude;
                if (sqr <= rangeSqr && sqr < minSqr)
                {
                    minSqr = sqr;
                    nearest = i;
                }
            }
            return nearest;
        }

        /// <summary>
        /// After 版: 2D グリッド分割による空間ハッシュ探索 O(k)
        /// cellSize = SearchRange に設定、原点周辺 3x3 セル (最大 9 セル) のみ走査
        /// </summary>
        private class SpatialGrid
        {
            private readonly Dictionary<(int, int), List<int>> _cells
                = new Dictionary<(int, int), List<int>>();
            private readonly float _cellSize;

            public SpatialGrid(Vector3[] positions, bool[] isDead, float cellSize)
            {
                _cellSize = cellSize;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (isDead[i]) continue;
                    var key = GetCellKey(positions[i]);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        _cells[key] = list;
                    }
                    list.Add(i);
                }
            }

            private (int, int) GetCellKey(Vector3 pos)
            {
                return ((int)Mathf.Floor(pos.x / _cellSize), (int)Mathf.Floor(pos.z / _cellSize));
            }

            public int FindNearest(
                Vector3 origin, float rangeSqr, Vector3[] positions, bool[] isDead)
            {
                var centerKey = GetCellKey(origin);
                int nearest = -1;
                float minSqr = float.MaxValue;

                // 周辺 3x3 セルのみ走査（cellSize == SearchRange なので検索範囲を包含）
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var key = (centerKey.Item1 + dx, centerKey.Item2 + dz);
                        if (!_cells.TryGetValue(key, out var list)) continue;

                        for (int k = 0; k < list.Count; k++)
                        {
                            int i = list[k];
                            if (isDead[i]) continue;
                            float sqr = (origin - positions[i]).sqrMagnitude;
                            if (sqr <= rangeSqr && sqr < minSqr)
                            {
                                minSqr = sqr;
                                nearest = i;
                            }
                        }
                    }
                }

                return nearest;
            }
        }
    }
}
