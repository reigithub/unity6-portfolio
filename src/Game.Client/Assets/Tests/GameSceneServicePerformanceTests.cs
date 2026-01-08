using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Scenes;
using Game.Core.Services;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// GameSceneServiceのUniTask版とTask版のパフォーマンス比較テスト
    /// Unity Profilerで確認するためのテストコード
    /// </summary>
    [TestFixture]
    public class GameSceneServicePerformanceTests
    {
        private const int IterationCount = 10000;
        private const int WarmupIterations = 10;
        private const int MemorySampleInterval = 100; // メモリサンプリング間隔

        private GameSceneServiceWithTask _taskService;
        private GameSceneService _uniTaskService;
        private MessageBrokerService _messageBrokerService;

        // Unity Profiler用のマーカー
        private static readonly ProfilerMarker TaskTransitionMarker = new("Task.TransitionAsync");
        private static readonly ProfilerMarker UniTaskTransitionMarker = new("UniTask.TransitionAsync");
        private static readonly ProfilerMarker TaskTerminateMarker = new("Task.TerminateAsync");
        private static readonly ProfilerMarker UniTaskTerminateMarker = new("UniTask.TerminateAsync");

        // メモリ計測用マーカー
        private static readonly ProfilerMarker TaskMemoryMarker = new("Task.Memory");
        private static readonly ProfilerMarker UniTaskMemoryMarker = new("UniTask.Memory");
        private static readonly ProfilerMarker TaskGCAllocMarker = new("Task.GCAlloc");
        private static readonly ProfilerMarker UniTaskGCAllocMarker = new("UniTask.GCAlloc");

        // ログ出力用
        private StringBuilder _logBuilder;
        private string _currentTestName;
        private DateTime _testStartTime;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // ログ初期化
            _logBuilder = new StringBuilder();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // ログファイル出力
            WriteLogFile();

            _logBuilder.Clear();
            _logBuilder = null;
        }

        [SetUp]
        public void SetUp()
        {
            _messageBrokerService = new MessageBrokerService();
            _messageBrokerService.Startup();
            _uniTaskService = new GameSceneService();
            _taskService = new GameSceneServiceWithTask();

            _currentTestName = TestContext.CurrentContext.Test.Name;
            _testStartTime = DateTime.Now;
            LogHeader();
        }

        [TearDown]
        public void TearDown()
        {
            _messageBrokerService?.Shutdown();
            _taskService?.Clear();
            LogFooter();
        }

        #region Log Helper Methods

        private void Log(string message)
        {
            UnityEngine.Debug.Log(message);
            _logBuilder?.AppendLine(message);
        }

        private void LogLine(string message)
        {
            _logBuilder?.AppendLine(message);
        }

        private void WriteLogFile()
        {
            if (_logBuilder == null || _logBuilder.Length == 0) return;

            try
            {
                // ログディレクトリの作成
                string logDirectory = $"{Application.dataPath}/Tests/TestLogs";
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // ファイル名生成 (GameSceneServicePerformanceTests_YYYY-MM-DD_hhmmss.log)
                string timestamp = _testStartTime.ToString("yyyy-MM-dd_HHmmss");
                // string safeTestName = _currentTestName.Replace("<", "_").Replace(">", "_").Replace(",", "_");
                string filePath = $"{logDirectory}/GameSceneServicePerformanceTests_{timestamp}.log";

                // ヘッダー追加
                var finalLog = new StringBuilder();
                finalLog.AppendLine("");
                finalLog.Append(_logBuilder);
                finalLog.AppendLine("");

                // ファイル書き込み
                File.WriteAllText(filePath, finalLog.ToString(), Encoding.UTF8);

                UnityEngine.Debug.Log($"ログファイル出力完了: {filePath}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"ログファイル出力エラー: {ex.Message}");
            }
        }

        private void LogHeader()
        {
            LogLine("================================================================================");
            LogLine($"GameSceneServicePerformanceTests ログレポート --- {_currentTestName}");
            LogLine("================================================================================");
            LogLine($"テスト名: {_currentTestName}");
            LogLine($"生成日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogLine($"イテレーション数: {IterationCount}");
            LogLine($"ウォームアップ回数: {WarmupIterations}");
            LogLine($"サンプリング間隔: {MemorySampleInterval}回毎");
            LogLine("================================================================================");
            LogLine($"テスト: {_currentTestName}");
            LogLine($"開始時刻: {_testStartTime:yyyy-MM-dd HH:mm:ss}");
            LogLine("================================================================================");
            LogLine("");
        }

        private void LogFooter()
        {
            // テスト終了情報を追加
            var endTime = DateTime.Now;
            var duration = endTime - _testStartTime;
            LogLine("");
            LogLine("--------------------------------------------------------------------------------");
            LogLine($"終了時刻: {endTime:yyyy-MM-dd HH:mm:ss}");
            LogLine($"実行時間: {duration.TotalSeconds:F2} 秒");
            LogLine("================================================================================");
            LogLine($"End of Report --- {_currentTestName}");
            LogLine("================================================================================");
            LogLine("");
        }

        #endregion

        #region GC Allocation Tests

        [Test]
        public async Task GCAllocation_Task_TransitionAsync()
        {
            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameScene>();
                await _taskService.TerminateLastAsync(clearHistory: true);
            }

            // GC計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            int startGcCount = GC.CollectionCount(0);

            using (TaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await _taskService.TransitionAsync<TaskMockGameScene>();
                    await _taskService.TerminateLastAsync(clearHistory: true);
                }
            }

            long endMemory = GC.GetTotalMemory(false);
            int endGcCount = GC.CollectionCount(0);

            long allocatedBytes = endMemory - startMemory;
            int gcCollections = endGcCount - startGcCount;

            Log($"[Task版] TransitionAsync x {IterationCount}回");
            Log($"  - 総アロケーション: {allocatedBytes:N0} bytes ({allocatedBytes / IterationCount:N0} bytes/回)");
            Log($"  - GC発生回数: {gcCollections}回");

            Assert.Pass($"Task版 GC Allocation: {allocatedBytes:N0} bytes, GC Collections: {gcCollections}");
        }

        [Test]
        public async Task GCAllocation_UniTask_TransitionAsync()
        {
            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await SimulateUniTaskTransition<UniTaskMockGameScene>();
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }

            // GC計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            int startGcCount = GC.CollectionCount(0);

            using (UniTaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await SimulateUniTaskTransition<UniTaskMockGameScene>();
                    await SimulateUniTaskTerminateLast(clearHistory: true);
                }
            }

            long endMemory = GC.GetTotalMemory(false);
            int endGcCount = GC.CollectionCount(0);

            long allocatedBytes = endMemory - startMemory;
            int gcCollections = endGcCount - startGcCount;

            Log($"[UniTask版] TransitionAsync x {IterationCount}回");
            Log($"  - 総アロケーション: {allocatedBytes:N0} bytes ({allocatedBytes / IterationCount:N0} bytes/回)");
            Log($"  - GC発生回数: {gcCollections}回");

            Assert.Pass($"UniTask版 GC Allocation: {allocatedBytes:N0} bytes, GC Collections: {gcCollections}");
        }

        #endregion

        #region CPU Performance Tests

        [Test]
        public async Task CPUPerformance_Task_TransitionAsync()
        {
            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameScene>();
                await _taskService.TerminateLastAsync(clearHistory: true);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (TaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await _taskService.TransitionAsync<TaskMockGameScene>();
                    await _taskService.TerminateLastAsync(clearHistory: true);
                }
            }

            stopwatch.Stop();

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double avgMs = totalMs / IterationCount;

            Log($"[Task版] TransitionAsync CPU時間");
            Log($"  - 総時間: {totalMs:F3} ms");
            Log($"  - 平均時間: {avgMs:F6} ms/回");
            Log($"  - スループット: {IterationCount / (totalMs / 1000):F0} ops/sec");

            Assert.Pass($"Task版 CPU Time: {totalMs:F3} ms total, {avgMs:F6} ms/op");
        }

        [Test]
        public async Task CPUPerformance_UniTask_TransitionAsync()
        {
            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await SimulateUniTaskTransition<UniTaskMockGameScene>();
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (UniTaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await SimulateUniTaskTransition<UniTaskMockGameScene>();
                    await SimulateUniTaskTerminateLast(clearHistory: true);
                }
            }

            stopwatch.Stop();

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double avgMs = totalMs / IterationCount;

            Log($"[UniTask版] TransitionAsync CPU時間");
            Log($"  - 総時間: {totalMs:F3} ms");
            Log($"  - 平均時間: {avgMs:F6} ms/回");
            Log($"  - スループット: {IterationCount / (totalMs / 1000):F0} ops/sec");

            Assert.Pass($"UniTask版 CPU Time: {totalMs:F3} ms total, {avgMs:F6} ms/op");
        }

        #endregion

        #region Combined Performance Tests

        [Test]
        public async Task Performance_Comparison_TransitionAsync()
        {
            Log("=== TransitionAsync パフォーマンス比較 ===");
            Log($"イテレーション数: {IterationCount}回");
            Log("");

            // Task版
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long taskStartMemory = GC.GetTotalMemory(true);
            var taskStopwatch = new Stopwatch();
            taskStopwatch.Start();

            using (TaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await _taskService.TransitionAsync<TaskMockGameScene>();
                    await _taskService.TerminateLastAsync(clearHistory: true);
                }
            }

            taskStopwatch.Stop();
            long taskEndMemory = GC.GetTotalMemory(false);
            long taskAllocated = taskEndMemory - taskStartMemory;
            double taskMs = taskStopwatch.Elapsed.TotalMilliseconds;

            // UniTask版
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long uniTaskStartMemory = GC.GetTotalMemory(true);
            var uniTaskStopwatch = new Stopwatch();
            uniTaskStopwatch.Start();

            using (UniTaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await SimulateUniTaskTransition<UniTaskMockGameScene>();
                    await SimulateUniTaskTerminateLast(clearHistory: true);
                }
            }

            uniTaskStopwatch.Stop();
            long uniTaskEndMemory = GC.GetTotalMemory(false);
            long uniTaskAllocated = uniTaskEndMemory - uniTaskStartMemory;
            double uniTaskMs = uniTaskStopwatch.Elapsed.TotalMilliseconds;

            // 結果出力
            Log("[Task版]");
            Log($"  - CPU時間: {taskMs:F3} ms ({taskMs / IterationCount:F6} ms/回)");
            Log($"  - メモリ: {taskAllocated:N0} bytes ({taskAllocated / IterationCount:N0} bytes/回)");
            Log("");
            Log("[UniTask版]");
            Log($"  - CPU時間: {uniTaskMs:F3} ms ({uniTaskMs / IterationCount:F6} ms/回)");
            Log($"  - メモリ: {uniTaskAllocated:N0} bytes ({uniTaskAllocated / IterationCount:N0} bytes/回)");
            Log("");
            Log("[比較]");
            Log($"  - CPU改善率: {(1 - uniTaskMs / taskMs) * 100:F1}% ({(taskMs / uniTaskMs):F2}x 高速)");
            Log($"  - メモリ改善率: {(1 - (double)uniTaskAllocated / taskAllocated) * 100:F1}% ({(double)taskAllocated / uniTaskAllocated:F2}x 削減)");

            Assert.Pass("パフォーマンス比較完了。Unity Consoleのログを確認してください。");
        }

        [Test]
        public async Task Performance_Comparison_WithArg()
        {
            Log("=== TransitionAsync<TScene, TArg> パフォーマンス比較 ===");
            Log($"イテレーション数: {IterationCount}回");
            Log("");

            var testArg = "TestArgument";

            // Task版
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long taskStartMemory = GC.GetTotalMemory(true);
            var taskStopwatch = new Stopwatch();
            taskStopwatch.Start();

            for (int i = 0; i < IterationCount; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameSceneWithArg<string>, string>(testArg);
                await _taskService.TerminateLastAsync(clearHistory: true);
            }

            taskStopwatch.Stop();
            long taskEndMemory = GC.GetTotalMemory(false);
            long taskAllocated = taskEndMemory - taskStartMemory;
            double taskMs = taskStopwatch.Elapsed.TotalMilliseconds;

            // UniTask版
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long uniTaskStartMemory = GC.GetTotalMemory(true);
            var uniTaskStopwatch = new Stopwatch();
            uniTaskStopwatch.Start();

            for (int i = 0; i < IterationCount; i++)
            {
                await SimulateUniTaskTransitionWithArg<UniTaskMockGameSceneWithArg<string>, string>(testArg);
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }

            uniTaskStopwatch.Stop();
            long uniTaskEndMemory = GC.GetTotalMemory(false);
            long uniTaskAllocated = uniTaskEndMemory - uniTaskStartMemory;
            double uniTaskMs = uniTaskStopwatch.Elapsed.TotalMilliseconds;

            // 結果出力
            Log("[Task版]");
            Log($"  - CPU時間: {taskMs:F3} ms ({taskMs / IterationCount:F6} ms/回)");
            Log($"  - メモリ: {taskAllocated:N0} bytes ({taskAllocated / IterationCount:N0} bytes/回)");
            Log("");
            Log("[UniTask版]");
            Log($"  - CPU時間: {uniTaskMs:F3} ms ({uniTaskMs / IterationCount:F6} ms/回)");
            Log($"  - メモリ: {uniTaskAllocated:N0} bytes ({uniTaskAllocated / IterationCount:N0} bytes/回)");
            Log("");

            if (taskMs > 0 && taskAllocated > 0)
            {
                Log("[比較]");
                Log($"  - CPU改善率: {(1 - uniTaskMs / taskMs) * 100:F1}%");
                Log($"  - メモリ改善率: {(1 - (double)uniTaskAllocated / taskAllocated) * 100:F1}%");
            }

            Assert.Pass("パフォーマンス比較完了。Unity Consoleのログを確認してください。");
        }

        #endregion

        #region Profiler Marker Tests (for Unity Profiler visualization)

        [Test]
        public async Task Profiler_Task_DetailedMarkers()
        {
            Log("Unity Profilerで Task.* マーカーを確認してください");

            using (TaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await _taskService.TransitionAsync<TaskMockGameScene>();
                }
            }

            using (TaskTerminateMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await _taskService.TerminateLastAsync(clearHistory: true);
                }
            }

            Assert.Pass("Profilerマーカーテスト完了");
        }

        [Test]
        public async Task Profiler_UniTask_DetailedMarkers()
        {
            Log("Unity Profilerで UniTask.* マーカーを確認してください");

            using (UniTaskTransitionMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await SimulateUniTaskTransition<UniTaskMockGameScene>();
                }
            }

            using (UniTaskTerminateMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await SimulateUniTaskTerminateLast(clearHistory: true);
                }
            }

            Assert.Pass("Profilerマーカーテスト完了");
        }

        #endregion

        #region Detailed Memory Visualization Tests

        /// <summary>
        /// メモリ使用量の詳細計測結果
        /// </summary>
        private struct MemoryMeasurement
        {
            public long TotalAllocatedBytes;
            public long Gen0Collections;
            public long Gen1Collections;
            public long Gen2Collections;
            public long PeakMemoryUsage;
            public long MinMemoryUsage;
            public List<long> MemorySamples;
            public double AverageMemoryPerIteration;
        }

        [Test]
        public async Task Memory_Detailed_Task_Analysis()
        {
            Log("=== Task版 詳細メモリ分析 ===");

            var measurement = await MeasureTaskMemoryDetailed();
            OutputMemoryMeasurement("Task", measurement);

            Assert.Pass("Task版メモリ分析完了");
        }

        [Test]
        public async Task Memory_Detailed_UniTask_Analysis()
        {
            Log("=== UniTask版 詳細メモリ分析 ===");

            var measurement = await MeasureUniTaskMemoryDetailed();
            OutputMemoryMeasurement("UniTask", measurement);

            Assert.Pass("UniTask版メモリ分析完了");
        }

        [Test]
        public async Task Memory_Detailed_Comparison()
        {
            Log("=== 詳細メモリ比較分析 ===");
            Log($"イテレーション数: {IterationCount}回");
            Log($"サンプリング間隔: {MemorySampleInterval}回毎");
            Log("");

            // Task版計測
            var taskMeasurement = await MeasureTaskMemoryDetailed();
            OutputMemoryMeasurement("Task", taskMeasurement);

            Log("");

            // UniTask版計測
            var uniTaskMeasurement = await MeasureUniTaskMemoryDetailed();
            OutputMemoryMeasurement("UniTask", uniTaskMeasurement);

            // 比較結果
            Log("");
            Log("=== 比較結果 ===");

            if (taskMeasurement.TotalAllocatedBytes > 0)
            {
                double memoryReduction = (1.0 - (double)uniTaskMeasurement.TotalAllocatedBytes / taskMeasurement.TotalAllocatedBytes) * 100;
                Log($"総メモリ削減率: {memoryReduction:F1}%");
                Log($"  Task: {FormatBytes(taskMeasurement.TotalAllocatedBytes)}");
                Log($"  UniTask: {FormatBytes(uniTaskMeasurement.TotalAllocatedBytes)}");
            }

            if (taskMeasurement.AverageMemoryPerIteration > 0)
            {
                double avgReduction = (1.0 - uniTaskMeasurement.AverageMemoryPerIteration / taskMeasurement.AverageMemoryPerIteration) * 100;
                Log($"平均メモリ/回 削減率: {avgReduction:F1}%");
                Log($"  Task: {taskMeasurement.AverageMemoryPerIteration:F2} bytes/回");
                Log($"  UniTask: {uniTaskMeasurement.AverageMemoryPerIteration:F2} bytes/回");
            }

            long taskTotalGC = taskMeasurement.Gen0Collections + taskMeasurement.Gen1Collections + taskMeasurement.Gen2Collections;
            long uniTaskTotalGC = uniTaskMeasurement.Gen0Collections + uniTaskMeasurement.Gen1Collections + uniTaskMeasurement.Gen2Collections;
            Log($"GCコレクション回数:");
            Log($"  Task: Gen0={taskMeasurement.Gen0Collections}, Gen1={taskMeasurement.Gen1Collections}, Gen2={taskMeasurement.Gen2Collections} (計{taskTotalGC})");
            Log($"  UniTask: Gen0={uniTaskMeasurement.Gen0Collections}, Gen1={uniTaskMeasurement.Gen1Collections}, Gen2={uniTaskMeasurement.Gen2Collections} (計{uniTaskTotalGC})");

            Assert.Pass("詳細メモリ比較完了");
        }

        [Test]
        public async Task Memory_Timeline_Visualization()
        {
            Log("=== メモリ使用量タイムライン ===");
            Log("(Unity Profilerで確認、またはConsoleログをCSVとして出力可能)");
            Log("");

            // Task版タイムライン
            Log("[Task版 メモリタイムライン]");
            var taskSamples = await CollectMemoryTimeline_Task();
            OutputMemoryTimeline("Task", taskSamples);

            Log("");

            // UniTask版タイムライン
            Log("[UniTask版 メモリタイムライン]");
            var uniTaskSamples = await CollectMemoryTimeline_UniTask();
            OutputMemoryTimeline("UniTask", uniTaskSamples);

            // CSV形式出力
            Log("");
            Log("=== CSV形式データ ===");
            OutputMemoryTimelineCSV(taskSamples, uniTaskSamples);

            // タイムライン詳細をログに追加
            AppendTimelineDetailsToLog(taskSamples, uniTaskSamples);

            Assert.Pass("メモリタイムライン出力完了");
        }

        [Test]
        public async Task Memory_PerOperation_Breakdown()
        {
            Log("=== 操作別メモリアロケーション内訳 ===");
            Log("");

            // Task版
            Log("[Task版]");
            await MeasureOperationMemory_Task();

            Log("");

            // UniTask版
            Log("[UniTask版]");
            await MeasureOperationMemory_UniTask();

            Assert.Pass("操作別メモリ分析完了");
        }

        private async Task<MemoryMeasurement> MeasureTaskMemoryDetailed()
        {
            var measurement = new MemoryMeasurement
            {
                MemorySamples = new List<long>()
            };

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameScene>();
                await _taskService.TerminateLastAsync(clearHistory: true);
            }

            // GC実行して安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);

            measurement.MinMemoryUsage = long.MaxValue;
            measurement.PeakMemoryUsage = 0;

            using (TaskMemoryMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    using (TaskGCAllocMarker.Auto())
                    {
                        await _taskService.TransitionAsync<TaskMockGameScene>();
                        await _taskService.TerminateLastAsync(clearHistory: true);
                    }

                    // 定期的にメモリサンプリング
                    if (i % MemorySampleInterval == 0)
                    {
                        long currentMemory = GC.GetTotalMemory(false);
                        measurement.MemorySamples.Add(currentMemory - startMemory);

                        if (currentMemory > measurement.PeakMemoryUsage)
                            measurement.PeakMemoryUsage = currentMemory;
                        if (currentMemory < measurement.MinMemoryUsage)
                            measurement.MinMemoryUsage = currentMemory;
                    }
                }
            }

            long endMemory = GC.GetTotalMemory(false);
            measurement.TotalAllocatedBytes = endMemory - startMemory;
            measurement.Gen0Collections = GC.CollectionCount(0) - startGen0;
            measurement.Gen1Collections = GC.CollectionCount(1) - startGen1;
            measurement.Gen2Collections = GC.CollectionCount(2) - startGen2;
            measurement.AverageMemoryPerIteration = (double)measurement.TotalAllocatedBytes / IterationCount;

            return measurement;
        }

        private async UniTask<MemoryMeasurement> MeasureUniTaskMemoryDetailed()
        {
            var measurement = new MemoryMeasurement
            {
                MemorySamples = new List<long>()
            };

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                await SimulateUniTaskTransition<UniTaskMockGameScene>();
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }

            // GC実行して安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);

            measurement.MinMemoryUsage = long.MaxValue;
            measurement.PeakMemoryUsage = 0;

            using (UniTaskMemoryMarker.Auto())
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    using (UniTaskGCAllocMarker.Auto())
                    {
                        await SimulateUniTaskTransition<UniTaskMockGameScene>();
                        await SimulateUniTaskTerminateLast(clearHistory: true);
                    }

                    // 定期的にメモリサンプリング
                    if (i % MemorySampleInterval == 0)
                    {
                        long currentMemory = GC.GetTotalMemory(false);
                        measurement.MemorySamples.Add(currentMemory - startMemory);

                        if (currentMemory > measurement.PeakMemoryUsage)
                            measurement.PeakMemoryUsage = currentMemory;
                        if (currentMemory < measurement.MinMemoryUsage)
                            measurement.MinMemoryUsage = currentMemory;
                    }
                }
            }

            long endMemory = GC.GetTotalMemory(false);
            measurement.TotalAllocatedBytes = endMemory - startMemory;
            measurement.Gen0Collections = GC.CollectionCount(0) - startGen0;
            measurement.Gen1Collections = GC.CollectionCount(1) - startGen1;
            measurement.Gen2Collections = GC.CollectionCount(2) - startGen2;
            measurement.AverageMemoryPerIteration = (double)measurement.TotalAllocatedBytes / IterationCount;

            return measurement;
        }

        private async Task<List<(int iteration, long memory)>> CollectMemoryTimeline_Task()
        {
            var samples = new List<(int iteration, long memory)>();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long baseMemory = GC.GetTotalMemory(true);

            for (int i = 0; i < IterationCount; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameScene>();
                await _taskService.TerminateLastAsync(clearHistory: true);

                if (i % MemorySampleInterval == 0)
                {
                    samples.Add((i, GC.GetTotalMemory(false) - baseMemory));
                }
            }

            return samples;
        }

        private async UniTask<List<(int iteration, long memory)>> CollectMemoryTimeline_UniTask()
        {
            var samples = new List<(int iteration, long memory)>();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long baseMemory = GC.GetTotalMemory(true);

            for (int i = 0; i < IterationCount; i++)
            {
                await SimulateUniTaskTransition<UniTaskMockGameScene>();
                await SimulateUniTaskTerminateLast(clearHistory: true);

                if (i % MemorySampleInterval == 0)
                {
                    samples.Add((i, GC.GetTotalMemory(false) - baseMemory));
                }
            }

            return samples;
        }

        private async Task MeasureOperationMemory_Task()
        {
            const int opIterations = 1000;

            // TransitionAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long transitionStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameScene>();
            }

            long transitionEnd = GC.GetTotalMemory(false);
            long transitionAlloc = transitionEnd - transitionStart;
            Log($"  TransitionAsync: {FormatBytes(transitionAlloc)} ({transitionAlloc / opIterations:N0} bytes/回)");

            // TerminateLastAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long terminateStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await _taskService.TerminateLastAsync(clearHistory: true);
            }

            long terminateEnd = GC.GetTotalMemory(false);
            long terminateAlloc = terminateEnd - terminateStart;
            Log($"  TerminateLastAsync: {FormatBytes(terminateAlloc)} ({terminateAlloc / opIterations:N0} bytes/回)");

            // 引数付きTransitionAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long argTransitionStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await _taskService.TransitionAsync<TaskMockGameSceneWithArg<string>, string>("test");
            }

            long argTransitionEnd = GC.GetTotalMemory(false);
            long argTransitionAlloc = argTransitionEnd - argTransitionStart;
            Log($"  TransitionAsync<TArg>: {FormatBytes(argTransitionAlloc)} ({argTransitionAlloc / opIterations:N0} bytes/回)");

            // クリーンアップ
            for (int i = 0; i < opIterations; i++)
            {
                await _taskService.TerminateLastAsync(clearHistory: true);
            }
        }

        private async UniTask MeasureOperationMemory_UniTask()
        {
            const int opIterations = 1000;

            // TransitionAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long transitionStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await SimulateUniTaskTransition<UniTaskMockGameScene>();
            }

            long transitionEnd = GC.GetTotalMemory(false);
            long transitionAlloc = transitionEnd - transitionStart;
            Log($"  TransitionAsync: {FormatBytes(transitionAlloc)} ({transitionAlloc / opIterations:N0} bytes/回)");

            // TerminateLastAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long terminateStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }

            long terminateEnd = GC.GetTotalMemory(false);
            long terminateAlloc = terminateEnd - terminateStart;
            Log($"  TerminateLastAsync: {FormatBytes(terminateAlloc)} ({terminateAlloc / opIterations:N0} bytes/回)");

            // 引数付きTransitionAsync計測
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long argTransitionStart = GC.GetTotalMemory(true);

            for (int i = 0; i < opIterations; i++)
            {
                await SimulateUniTaskTransitionWithArg<UniTaskMockGameSceneWithArg<string>, string>("test");
            }

            long argTransitionEnd = GC.GetTotalMemory(false);
            long argTransitionAlloc = argTransitionEnd - argTransitionStart;
            Log($"  TransitionAsync<TArg>: {FormatBytes(argTransitionAlloc)} ({argTransitionAlloc / opIterations:N0} bytes/回)");

            // クリーンアップ
            for (int i = 0; i < opIterations; i++)
            {
                await SimulateUniTaskTerminateLast(clearHistory: true);
            }
        }

        private void OutputMemoryMeasurement(string label, MemoryMeasurement measurement)
        {
            Log($"[{label}版 メモリ計測結果]");
            Log($"  総アロケーション: {FormatBytes(measurement.TotalAllocatedBytes)}");
            Log($"  平均/回: {measurement.AverageMemoryPerIteration:F2} bytes");
            Log($"  ピークメモリ: {FormatBytes(measurement.PeakMemoryUsage)}");
            Log($"  GC回数: Gen0={measurement.Gen0Collections}, Gen1={measurement.Gen1Collections}, Gen2={measurement.Gen2Collections}");

            if (measurement.MemorySamples.Count > 0)
            {
                long maxSample = 0;
                long minSample = long.MaxValue;
                long sumSample = 0;
                foreach (var sample in measurement.MemorySamples)
                {
                    if (sample > maxSample) maxSample = sample;
                    if (sample < minSample) minSample = sample;
                    sumSample += sample;
                }

                double avgSample = (double)sumSample / measurement.MemorySamples.Count;
                Log($"  サンプル統計: 最小={FormatBytes(minSample)}, 最大={FormatBytes(maxSample)}, 平均={FormatBytes((long)avgSample)}");
            }
        }

        private void OutputMemoryTimeline(string label, List<(int iteration, long memory)> samples)
        {
            if (samples.Count == 0) return;

            // 簡易ASCIIグラフ
            long maxMemory = 0;
            foreach (var s in samples)
            {
                if (s.memory > maxMemory) maxMemory = s.memory;
            }

            if (maxMemory <= 0) maxMemory = 1;

            const int graphWidth = 50;
            Log($"メモリ推移グラフ (最大: {FormatBytes(maxMemory)}):");

            int step = Math.Max(1, samples.Count / 10);
            for (int i = 0; i < samples.Count; i += step)
            {
                var sample = samples[i];
                int barLength = (int)((double)sample.memory / maxMemory * graphWidth);
                barLength = Math.Max(0, Math.Min(graphWidth, barLength));
                string bar = new string('#', barLength) + new string('-', graphWidth - barLength);
                Log($"  [{sample.iteration,5}] {bar} {FormatBytes(sample.memory)}");
            }
        }

        private void OutputMemoryTimelineCSV(
            List<(int iteration, long memory)> taskSamples,
            List<(int iteration, long memory)> uniTaskSamples)
        {
            LogLine("Iteration,Task_Memory_Bytes,UniTask_Memory_Bytes");

            int maxCount = Math.Max(taskSamples.Count, uniTaskSamples.Count);
            for (int i = 0; i < maxCount; i++)
            {
                int iteration = i * MemorySampleInterval;
                long taskMem = i < taskSamples.Count ? taskSamples[i].memory : 0;
                long uniTaskMem = i < uniTaskSamples.Count ? uniTaskSamples[i].memory : 0;
                LogLine($"{iteration},{taskMem},{uniTaskMem}");
            }
        }

        private void AppendTimelineDetailsToLog(
            List<(int iteration, long memory)> taskSamples,
            List<(int iteration, long memory)> uniTaskSamples)
        {
            LogLine("");
            LogLine("--------------------------------------------------------------------------------");
            LogLine("[タイムライン詳細サマリー]");
            LogLine("--------------------------------------------------------------------------------");

            // Task版サマリー
            if (taskSamples.Count > 0)
            {
                var taskStats = CalculateStats(taskSamples);
                LogLine("[Task版]");
                LogLine($"  サンプル数: {taskSamples.Count}");
                LogLine($"  最小メモリ: {FormatBytes(taskStats.min)}");
                LogLine($"  最大メモリ: {FormatBytes(taskStats.max)}");
                LogLine($"  平均メモリ: {FormatBytes((long)taskStats.avg)}");
                LogLine($"  最終メモリ: {FormatBytes(taskSamples[taskSamples.Count - 1].memory)}");
                LogLine("");
            }

            // UniTask版サマリー
            if (uniTaskSamples.Count > 0)
            {
                var uniTaskStats = CalculateStats(uniTaskSamples);
                LogLine("[UniTask版]");
                LogLine($"  サンプル数: {uniTaskSamples.Count}");
                LogLine($"  最小メモリ: {FormatBytes(uniTaskStats.min)}");
                LogLine($"  最大メモリ: {FormatBytes(uniTaskStats.max)}");
                LogLine($"  平均メモリ: {FormatBytes((long)uniTaskStats.avg)}");
                LogLine($"  最終メモリ: {FormatBytes(uniTaskSamples[uniTaskSamples.Count - 1].memory)}");
                LogLine("");
            }

            // 比較結果
            if (taskSamples.Count > 0 && uniTaskSamples.Count > 0)
            {
                var taskStats = CalculateStats(taskSamples);
                var uniTaskStats = CalculateStats(uniTaskSamples);

                LogLine("[比較結果]");
                if (taskStats.max > 0)
                {
                    double maxReduction = (1.0 - (double)uniTaskStats.max / taskStats.max) * 100;
                    LogLine($"  最大メモリ削減率: {maxReduction:F1}%");
                }

                if (taskStats.avg > 0)
                {
                    double avgReduction = (1.0 - uniTaskStats.avg / taskStats.avg) * 100;
                    LogLine($"  平均メモリ削減率: {avgReduction:F1}%");
                }
            }
        }

        private (long min, long max, double avg) CalculateStats(List<(int iteration, long memory)> samples)
        {
            if (samples.Count == 0) return (0, 0, 0);

            long min = long.MaxValue;
            long max = 0;
            long sum = 0;

            foreach (var sample in samples)
            {
                if (sample.memory < min) min = sample.memory;
                if (sample.memory > max) max = sample.memory;
                sum += sample.memory;
            }

            return (min, max, (double)sum / samples.Count);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return $"{bytes:N0} B";
            if (bytes < 1024) return $"{bytes:N0} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        #endregion

        #region Helper Methods

        private readonly System.Collections.Generic.LinkedList<IGameScene> _uniTaskScenes = new();

        private async UniTask SimulateUniTaskTransition<TScene>() where TScene : IGameScene, new()
        {
            var gameScene = new TScene();
            _uniTaskScenes.AddLast(gameScene);
            await SimulateTransitionCore(gameScene);
        }

        private async UniTask SimulateUniTaskTransitionWithArg<TScene, TArg>(TArg arg)
            where TScene : IGameScene, new()
        {
            var gameScene = new TScene();
            if (gameScene is IGameSceneArgHandler handler)
            {
                handler.ArgHandler = scene =>
                {
                    if (scene is IGameSceneArg<TArg> gameSceneArg)
                        return gameSceneArg.ArgHandle(arg);
                    return UniTask.CompletedTask;
                };
            }

            _uniTaskScenes.AddLast(gameScene);
            await SimulateTransitionCore(gameScene);
        }

        private async UniTask SimulateTransitionCore(IGameScene gameScene)
        {
            gameScene.State = GameSceneState.Processing;

            if (gameScene.ArgHandler != null)
                await gameScene.ArgHandler.Invoke(gameScene);

            await gameScene.PreInitialize();
            await gameScene.LoadAsset();
            await gameScene.Startup();
            await gameScene.Ready();
        }

        private async UniTask SimulateUniTaskTerminateLast(bool clearHistory = false)
        {
            var currentNode = _uniTaskScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    gameScene.State = GameSceneState.Terminate;
                    await gameScene.Terminate();

                    if (clearHistory) _uniTaskScenes.Remove(currentNode);
                }
            }
        }

        #endregion
    }

    #region UniTask版モッククラス（パフォーマンステスト用）

    public class UniTaskMockGameScene : IGameScene
    {
        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }

        public UniTask PreInitialize() => UniTask.CompletedTask;
        public UniTask LoadAsset() => UniTask.CompletedTask;
        public UniTask Startup() => UniTask.CompletedTask;
        public UniTask Ready() => UniTask.CompletedTask;
        public UniTask Sleep() => UniTask.CompletedTask;
        public UniTask Restart() => UniTask.CompletedTask;
        public UniTask Terminate() => UniTask.CompletedTask;
    }

    public class UniTaskMockGameSceneWithArg<TArg> : IGameScene, IGameSceneArg<TArg>
    {
        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }
        public TArg ReceivedArg { get; private set; }

        public UniTask PreInitialize() => UniTask.CompletedTask;
        public UniTask LoadAsset() => UniTask.CompletedTask;
        public UniTask Startup() => UniTask.CompletedTask;
        public UniTask Ready() => UniTask.CompletedTask;
        public UniTask Sleep() => UniTask.CompletedTask;
        public UniTask Restart() => UniTask.CompletedTask;
        public UniTask Terminate() => UniTask.CompletedTask;

        public UniTask ArgHandle(TArg arg)
        {
            ReceivedArg = arg;
            return UniTask.CompletedTask;
        }
    }

    #endregion
}