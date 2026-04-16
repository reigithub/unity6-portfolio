using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.MVP.Weapon
{
    /// <summary>
    /// L1-3 プロジェクタイル移動計算の Layer 1 (EditMode) パフォーマンスベンチ。
    /// SurvivorProjectile.Update の移動部分 (pos += vel * dt + lifetime decay) を抽出し、
    /// Before: Vector3[] 逐次 for ループ vs After: NativeArray<float3> + IJobParallelFor + Burst を比較する。
    ///
    /// Layer 1 の位置付け: Burst SIMD + Job 並列化の効果検証。
    /// SphereCast / ホーミング (Slerp) / Transform は Layer 2 回送。
    /// </summary>
    [TestFixture]
    public class ProjectileMovementPerformanceTests
    {
        private const int Seed = 42;
        private static readonly int[] ProjectileCounts = { 50, 100, 200, 500, 1000, 5000, 10000, 50000 };
        private const int WarmupIterations = 100;
        private const int MeasureIterations = 1000;
        private const float DeltaTime = 0.0167f;
        private const int InnerLoopBatchCount = 64;
        private const float SpawnHalfExtent = 50f;
        private const float SpeedMin = 5f;
        private const float SpeedMax = 10f;
        private const float LifetimeMin = 1f;
        private const float LifetimeMax = 3f;

        private StringBuilder _logBuilder;
        private string _logFilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir,
                $"ProjectileMovementPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.log");
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
        public void ProjectileMove_SequentialVsBurstJob([ValueSource(nameof(ProjectileCounts))] int count)
        {
            // --- Sequential (Before) ---
            var seqPositions = new Vector3[count];
            var seqDirections = new Vector3[count];
            var seqSpeeds = new float[count];
            var seqLifetimes = new float[count];
            var seqIsActive = new bool[count];
            GenerateProjectilesManaged(count, Seed, seqPositions, seqDirections, seqSpeeds, seqLifetimes, seqIsActive);

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                UpdateProjectilesSequential(seqPositions, seqDirections, seqSpeeds, seqLifetimes, seqIsActive, DeltaTime);
            }

            // Reset state for measurement
            GenerateProjectilesManaged(count, Seed, seqPositions, seqDirections, seqSpeeds, seqLifetimes, seqIsActive);

            // Measure
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                UpdateProjectilesSequential(seqPositions, seqDirections, seqSpeeds, seqLifetimes, seqIsActive, DeltaTime);
            }
            sw.Stop();
            var seqMs = sw.Elapsed.TotalMilliseconds;
            var seqAlloc = GC.GetTotalMemory(false) - memBefore;

            // --- Burst Job (After) ---
            var jobPositions = new NativeArray<float3>(count, Allocator.TempJob);
            var jobDirections = new NativeArray<float3>(count, Allocator.TempJob);
            var jobSpeeds = new NativeArray<float>(count, Allocator.TempJob);
            var jobLifetimes = new NativeArray<float>(count, Allocator.TempJob);
            var jobIsActive = new NativeArray<byte>(count, Allocator.TempJob);

            try
            {
                GenerateProjectilesNative(count, Seed, jobPositions, jobDirections, jobSpeeds, jobLifetimes, jobIsActive);

                // Warmup (Burst JIT 込み)
                for (int w = 0; w < WarmupIterations; w++)
                {
                    new MoveProjectilesJob
                    {
                        Positions = jobPositions,
                        Directions = jobDirections,
                        Speeds = jobSpeeds,
                        Lifetimes = jobLifetimes,
                        IsActive = jobIsActive,
                        DeltaTime = DeltaTime
                    }.Schedule(count, InnerLoopBatchCount).Complete();
                }

                // Reset for measurement
                GenerateProjectilesNative(count, Seed, jobPositions, jobDirections, jobSpeeds, jobLifetimes, jobIsActive);

                // Measure
                GC.Collect();
                GC.WaitForPendingFinalizers();
                memBefore = GC.GetTotalMemory(true);
                sw.Restart();
                for (int iter = 0; iter < MeasureIterations; iter++)
                {
                    new MoveProjectilesJob
                    {
                        Positions = jobPositions,
                        Directions = jobDirections,
                        Speeds = jobSpeeds,
                        Lifetimes = jobLifetimes,
                        IsActive = jobIsActive,
                        DeltaTime = DeltaTime
                    }.Schedule(count, InnerLoopBatchCount).Complete();
                }
                sw.Stop();
                var jobMs = sw.Elapsed.TotalMilliseconds;
                var jobAlloc = GC.GetTotalMemory(false) - memBefore;

                // --- Log ---
                double perSeqUs = seqMs * 1000.0 / MeasureIterations / count;
                double perJobUs = jobMs * 1000.0 / MeasureIterations / count;
                double speedup = jobMs > 0 ? seqMs / jobMs : 0;

                _logBuilder.AppendLine($"[ProjectileMove SequentialVsBurstJob] n={count}");
                _logBuilder.AppendLine($"  Sequential : {seqMs:F2}ms total / {perSeqUs:F3}us per entity / {seqAlloc:N0} bytes alloc");
                _logBuilder.AppendLine($"  BurstJob   : {jobMs:F2}ms total / {perJobUs:F3}us per entity / {jobAlloc:N0} bytes alloc");
                _logBuilder.AppendLine($"  Speedup    : {speedup:F2}x");
            }
            finally
            {
                jobPositions.Dispose();
                jobDirections.Dispose();
                jobSpeeds.Dispose();
                jobLifetimes.Dispose();
                jobIsActive.Dispose();
            }
        }

        // ---------------------------------------------------------------
        // Sequential (Before)
        // ---------------------------------------------------------------

        private static void UpdateProjectilesSequential(
            Vector3[] positions, Vector3[] directions, float[] speeds,
            float[] lifetimes, bool[] isActive, float dt)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (!isActive[i]) continue;
                positions[i] += directions[i] * (speeds[i] * dt);
                lifetimes[i] -= dt;
                isActive[i] = lifetimes[i] > 0f;
            }
        }

        // ---------------------------------------------------------------
        // Burst Job (After)
        // ---------------------------------------------------------------

        [BurstCompile]
        private struct MoveProjectilesJob : IJobParallelFor
        {
            public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float3> Directions;
            [ReadOnly] public NativeArray<float> Speeds;
            public NativeArray<float> Lifetimes;
            public NativeArray<byte> IsActive; // byte で bool 代替（Blittable 安定性）
            public float DeltaTime;

            public void Execute(int index)
            {
                if (IsActive[index] == 0) return;
                Positions[index] += Directions[index] * (Speeds[index] * DeltaTime);
                Lifetimes[index] -= DeltaTime;
                IsActive[index] = Lifetimes[index] > 0f ? (byte)1 : (byte)0;
            }
        }

        // ---------------------------------------------------------------
        // Data generation
        // ---------------------------------------------------------------

        private static void GenerateProjectilesManaged(
            int count, int seed,
            Vector3[] positions, Vector3[] directions, float[] speeds,
            float[] lifetimes, bool[] isActive)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                directions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                speeds[i] = SpeedMin + (float)rng.NextDouble() * (SpeedMax - SpeedMin);
                lifetimes[i] = LifetimeMin + (float)rng.NextDouble() * (LifetimeMax - LifetimeMin);
                isActive[i] = true;
            }
        }

        private static void GenerateProjectilesNative(
            int count, int seed,
            NativeArray<float3> positions, NativeArray<float3> directions,
            NativeArray<float> speeds, NativeArray<float> lifetimes,
            NativeArray<byte> isActive)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                positions[i] = new float3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * SpawnHalfExtent);
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                directions[i] = new float3(math.cos(angle), 0f, math.sin(angle));
                speeds[i] = SpeedMin + (float)rng.NextDouble() * (SpeedMax - SpeedMin);
                lifetimes[i] = LifetimeMin + (float)rng.NextDouble() * (LifetimeMax - LifetimeMin);
                isActive[i] = 1;
            }
        }
    }
}
