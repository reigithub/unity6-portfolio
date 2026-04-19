using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.Library.Shared.Dto;
using Game.Shared.Network.Survivor;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.MVP.Enemy
{
    /// <summary>
    /// L1-4 敵状態同期配列 alloc の Layer 1 (EditMode) パフォーマンスベンチ。
    /// SurvivorEnemySpawner.SyncEnemyStatesToNetwork の `new SurvivorNetworkEnemyStateSnapshot[N]`
    /// 部分を抽出し、Before: 毎回新規配列 vs After: 事前確保バッファ + int count を比較する。
    ///
    /// Layer 1 の位置付け: GC Alloc 削減の検証。Burst / Physics 依存なし。
    /// 本番 `WriteEnemyStates(T[])` API を `WriteEnemyStates(T[], int count)` に拡張する
    /// 最適化が適用可能であることの事前検証（本番反映は別 PR）。
    /// </summary>
    [TestFixture]
    public class SyncEnemyStatesAllocationPerformanceTests
    {
        private const int Seed = 42;
        private static readonly int[] EnemyCounts = { 32, 100, 256, 512 };
        private const int WarmupIterations = 100;
        private const int MeasureIterations = 1000;
        private const int MaxEnemies = 512;
        private const float SpawnHalfExtent = 50f;

        private StringBuilder _logBuilder;
        private string _logFilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir,
                $"SyncEnemyStatesAllocationPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.log");
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
        public void Snapshot_NewArrayVsBufferReuse([ValueSource(nameof(EnemyCounts))] int n)
        {
            var enemies = GenerateMockEnemies(n, Seed);
            var buffer = new SurvivorNetworkEnemyStateSnapshot[MaxEnemies];

            // --- Warmup (Before: new array) ---
            for (int w = 0; w < WarmupIterations; w++)
            {
                var _ = AllocateNewArray(enemies);
            }

            // --- Measure (Before: new array) ---
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                var _ = AllocateNewArray(enemies);
            }
            sw.Stop();
            var newArrayMs = sw.Elapsed.TotalMilliseconds;
            var newArrayAlloc = GC.GetTotalMemory(false) - memBefore;

            // --- Warmup (After: buffer reuse) ---
            for (int w = 0; w < WarmupIterations; w++)
            {
                FillPreAllocatedBuffer(buffer, enemies);
            }

            // --- Measure (After: buffer reuse) ---
            GC.Collect();
            GC.WaitForPendingFinalizers();
            memBefore = GC.GetTotalMemory(true);
            sw.Restart();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                FillPreAllocatedBuffer(buffer, enemies);
            }
            sw.Stop();
            var bufferMs = sw.Elapsed.TotalMilliseconds;
            var bufferAlloc = GC.GetTotalMemory(false) - memBefore;

            // --- Log ---
            double perNewUs = newArrayMs * 1000.0 / MeasureIterations;
            double perBufferUs = bufferMs * 1000.0 / MeasureIterations;
            double speedup = bufferMs > 0 ? newArrayMs / bufferMs : 0;
            double allocReduction = newArrayAlloc > 0
                ? (1.0 - (double)Math.Max(0, bufferAlloc) / newArrayAlloc) * 100.0
                : 0;

            _logBuilder.AppendLine($"[Snapshot NewArrayVsBufferReuse] n={n}");
            _logBuilder.AppendLine($"  new[]  : {newArrayMs:F2}ms total / {perNewUs:F2}us per call / {newArrayAlloc:N0} bytes alloc");
            _logBuilder.AppendLine($"  buffer : {bufferMs:F2}ms total / {perBufferUs:F2}us per call / {bufferAlloc:N0} bytes alloc");
            _logBuilder.AppendLine($"  Speedup: {speedup:F2}x");
            _logBuilder.AppendLine($"  AllocReduction: {allocReduction:F1}%");
        }

        // ---------------------------------------------------------------
        // Data generation
        // ---------------------------------------------------------------

        /// <summary>
        /// 実 SurvivorEnemyController の位置・速度・HP 参照を代替する値型 struct。
        /// struct 自身が alloc を発生させないよう readonly で定義。
        /// </summary>
        private readonly struct MockEnemy
        {
            public readonly int EnemyId;
            public readonly int CurrentHp;
            public readonly Vector3 Position;
            public readonly Vector3 Velocity;

            public MockEnemy(int enemyId, int hp, Vector3 pos, Vector3 vel)
            {
                EnemyId = enemyId;
                CurrentHp = hp;
                Position = pos;
                Velocity = vel;
            }
        }

        private static MockEnemy[] GenerateMockEnemies(int count, int seed)
        {
            var rng = new System.Random(seed);
            var arr = new MockEnemy[count];
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
                var vel = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0),
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0));
                arr[i] = new MockEnemy(enemyId: i + 1, hp: 100, pos: pos, vel: vel);
            }
            return arr;
        }

        // ---------------------------------------------------------------
        // Before: 毎回 new array 確保
        // ---------------------------------------------------------------

        private static SurvivorNetworkEnemyStateSnapshot[] AllocateNewArray(MockEnemy[] enemies)
        {
            var snapshots = new SurvivorNetworkEnemyStateSnapshot[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                snapshots[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = i,
                    EnemyMasterId = e.EnemyId,
                    PositionX = e.Position.x,
                    PositionY = e.Position.y,
                    PositionZ = e.Position.z,
                    VelocityX = e.Velocity.x,
                    VelocityY = e.Velocity.y,
                    VelocityZ = e.Velocity.z,
                    CurrentHp = e.CurrentHp,
                    SyncType = EnemySyncType.PositionUpdate
                };
            }
            return snapshots;
        }

        // ---------------------------------------------------------------
        // After: 事前確保バッファ + int count
        // ---------------------------------------------------------------

        private static int FillPreAllocatedBuffer(
            SurvivorNetworkEnemyStateSnapshot[] buffer, MockEnemy[] enemies)
        {
            int count = Math.Min(enemies.Length, buffer.Length);
            for (int i = 0; i < count; i++)
            {
                var e = enemies[i];
                buffer[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = i,
                    EnemyMasterId = e.EnemyId,
                    PositionX = e.Position.x,
                    PositionY = e.Position.y,
                    PositionZ = e.Position.z,
                    VelocityX = e.Velocity.x,
                    VelocityY = e.Velocity.y,
                    VelocityZ = e.Velocity.z,
                    CurrentHp = e.CurrentHp,
                    SyncType = EnemySyncType.PositionUpdate
                };
            }
            return count;
        }
    }
}
