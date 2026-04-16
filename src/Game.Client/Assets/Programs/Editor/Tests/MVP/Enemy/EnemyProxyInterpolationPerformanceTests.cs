using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.MVP.Survivor.Enemy;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.MVP.Enemy
{
    /// <summary>
    /// EnemyProxyInterpolation 単体性能ベンチ（Mono 版ベースライン）。
    /// ECS 化前の現状値を取得し、After 比較の基準点を確定させる。
    /// 雛形は EcsEnemyPerformanceTests.cs に準拠。
    /// </summary>
    [TestFixture]
    public class EnemyProxyInterpolationPerformanceTests
    {
        private const int WarmupIterations = 100;
        private const int MeasureIterations = 1000;
        private const float DeltaTime = 0.016f;
        private const float CorrectionDecayRate = 10f;
        private const float MaxCorrectionDistance = 3f;

        private StringBuilder _logBuilder;
        private string _logFilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir,
                $"EnemyProxyInterpolationPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.log");
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

        /// <summary>
        /// GetPosition (毎フレーム補間計算) の Mono 版ベースライン計測。
        /// 期待: GC Alloc = 0、N に対して線形スケール。
        /// </summary>
        [Test]
        public void Interpolation_GetPosition_BaselineMono([Values(100, 256, 500, 512)] int n)
        {
            var interps = CreateInterpolations(n);

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                for (int i = 0; i < n; i++)
                {
                    interps[i].GetPosition(DeltaTime, CorrectionDecayRate);
                }
            }

            // Measure
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore = GC.GetTotalMemory(true);

            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    interps[i].GetPosition(DeltaTime, CorrectionDecayRate);
                }
            }
            sw.Stop();

            var memAfter = GC.GetTotalMemory(false) - memBefore;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameMs = totalMs / MeasureIterations;
            double perEntityUs = totalMs * 1000.0 / MeasureIterations / n;

            _logBuilder.AppendLine($"[Interpolation.GetPosition Mono Baseline] n={n}");
            _logBuilder.AppendLine($"  Total: {totalMs:F2}ms over {MeasureIterations} iterations");
            _logBuilder.AppendLine($"  Per frame: {perFrameMs:F4}ms");
            _logBuilder.AppendLine($"  Per entity: {perEntityUs:F3}us");
            _logBuilder.AppendLine($"  GC Alloc: {memAfter:N0} bytes");

            // GC Alloc は記録のみ（Editor バックグラウンド処理由来のノイズで Strict 0 アサート不可）。
            // 補間ロジック自体は pure struct 演算で alloc 発生源なし、After 比較で同条件相対値を取得する。
        }

        /// <summary>
        /// OnSyncReceived (ネットワーク受信時の補間状態更新) の Mono 版ベースライン計測。
        /// </summary>
        [Test]
        public void Interpolation_OnSyncReceived_BaselineMono([Values(100, 256, 500, 512)] int n)
        {
            var interps = CreateInterpolations(n);
            var newPositions = new Vector3[n];
            var newVelocities = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                newPositions[i] = new Vector3(i * 5f + 1f, 0, i * 3f + 1f);
                newVelocities[i] = new Vector3(2f, 0, 1f);
            }

            // Warmup
            for (int w = 0; w < WarmupIterations; w++)
            {
                for (int i = 0; i < n; i++)
                {
                    interps[i].OnSyncReceived(newPositions[i], newVelocities[i], MaxCorrectionDistance);
                }
            }

            // Measure
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore = GC.GetTotalMemory(true);

            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < MeasureIterations; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    interps[i].OnSyncReceived(newPositions[i], newVelocities[i], MaxCorrectionDistance);
                }
            }
            sw.Stop();

            var memAfter = GC.GetTotalMemory(false) - memBefore;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameMs = totalMs / MeasureIterations;
            double perEntityUs = totalMs * 1000.0 / MeasureIterations / n;

            _logBuilder.AppendLine($"[Interpolation.OnSyncReceived Mono Baseline] n={n}");
            _logBuilder.AppendLine($"  Total: {totalMs:F2}ms over {MeasureIterations} iterations");
            _logBuilder.AppendLine($"  Per frame: {perFrameMs:F4}ms");
            _logBuilder.AppendLine($"  Per entity: {perEntityUs:F3}us");
            _logBuilder.AppendLine($"  GC Alloc: {memAfter:N0} bytes");

            // GC Alloc は記録のみ（Editor バックグラウンド処理由来のノイズで Strict 0 アサート不可）。
        }

        private static EnemyProxyInterpolation[] CreateInterpolations(int n)
        {
            var arr = new EnemyProxyInterpolation[n];
            for (int i = 0; i < n; i++)
            {
                arr[i] = new EnemyProxyInterpolation
                {
                    LastSyncPosition = new Vector3(i * 5f, 0f, i * 3f),
                    Velocity = new Vector3(1f, 0f, 0.5f),
                    TimeSinceSync = 0.016f,
                    CorrectionOffset = Vector3.zero
                };
            }
            return arr;
        }
    }
}
