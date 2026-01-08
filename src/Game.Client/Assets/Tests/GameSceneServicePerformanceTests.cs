using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Scenes;
using Game.Core.Services;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

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

        private GameSceneServiceWithTask _taskService;
        private GameSceneService _uniTaskService;
        private MessageBrokerService _messageBrokerService;

        // Unity Profiler用のマーカー
        private static readonly ProfilerMarker TaskTransitionMarker = new("Task.TransitionAsync");
        private static readonly ProfilerMarker UniTaskTransitionMarker = new("UniTask.TransitionAsync");
        private static readonly ProfilerMarker TaskTerminateMarker = new("Task.TerminateAsync");
        private static readonly ProfilerMarker UniTaskTerminateMarker = new("UniTask.TerminateAsync");

        [SetUp]
        public void SetUp()
        {
            _taskService = new GameSceneServiceWithTask();

            GameServiceManager.Instance.StartUp();
            GameServiceManager.Instance.StartupService<MessageBrokerService>();
            _messageBrokerService = GameServiceManager.Instance.GetService<MessageBrokerService>();
            _uniTaskService = GameServiceManager.Instance.GetService<GameSceneService>();
        }

        [TearDown]
        public void TearDown()
        {
            _taskService?.Clear();
            GameServiceManager.Instance.Shutdown();
        }

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

            Debug.Log($"[Task版] TransitionAsync x {IterationCount}回");
            Debug.Log($"  - 総アロケーション: {allocatedBytes:N0} bytes ({allocatedBytes / IterationCount:N0} bytes/回)");
            Debug.Log($"  - GC発生回数: {gcCollections}回");

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

            Debug.Log($"[UniTask版] TransitionAsync x {IterationCount}回");
            Debug.Log($"  - 総アロケーション: {allocatedBytes:N0} bytes ({allocatedBytes / IterationCount:N0} bytes/回)");
            Debug.Log($"  - GC発生回数: {gcCollections}回");

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

            Debug.Log($"[Task版] TransitionAsync CPU時間");
            Debug.Log($"  - 総時間: {totalMs:F3} ms");
            Debug.Log($"  - 平均時間: {avgMs:F6} ms/回");
            Debug.Log($"  - スループット: {IterationCount / (totalMs / 1000):F0} ops/sec");

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

            Debug.Log($"[UniTask版] TransitionAsync CPU時間");
            Debug.Log($"  - 総時間: {totalMs:F3} ms");
            Debug.Log($"  - 平均時間: {avgMs:F6} ms/回");
            Debug.Log($"  - スループット: {IterationCount / (totalMs / 1000):F0} ops/sec");

            Assert.Pass($"UniTask版 CPU Time: {totalMs:F3} ms total, {avgMs:F6} ms/op");
        }

        #endregion

        #region Combined Performance Tests

        [Test]
        public async Task Performance_Comparison_TransitionAsync()
        {
            Debug.Log("=== TransitionAsync パフォーマンス比較 ===");
            Debug.Log($"イテレーション数: {IterationCount}回");
            Debug.Log("");

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
            Debug.Log("[Task版]");
            Debug.Log($"  - CPU時間: {taskMs:F3} ms ({taskMs / IterationCount:F6} ms/回)");
            Debug.Log($"  - メモリ: {taskAllocated:N0} bytes ({taskAllocated / IterationCount:N0} bytes/回)");
            Debug.Log("");
            Debug.Log("[UniTask版]");
            Debug.Log($"  - CPU時間: {uniTaskMs:F3} ms ({uniTaskMs / IterationCount:F6} ms/回)");
            Debug.Log($"  - メモリ: {uniTaskAllocated:N0} bytes ({uniTaskAllocated / IterationCount:N0} bytes/回)");
            Debug.Log("");
            Debug.Log("[比較]");
            Debug.Log($"  - CPU改善率: {(1 - uniTaskMs / taskMs) * 100:F1}% ({(taskMs / uniTaskMs):F2}x 高速)");
            Debug.Log($"  - メモリ改善率: {(1 - (double)uniTaskAllocated / taskAllocated) * 100:F1}% ({(double)taskAllocated / uniTaskAllocated:F2}x 削減)");

            Assert.Pass("パフォーマンス比較完了。Unity Consoleのログを確認してください。");
        }

        [Test]
        public async Task Performance_Comparison_WithArg()
        {
            Debug.Log("=== TransitionAsync<TScene, TArg> パフォーマンス比較 ===");
            Debug.Log($"イテレーション数: {IterationCount}回");
            Debug.Log("");

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
            Debug.Log("[Task版]");
            Debug.Log($"  - CPU時間: {taskMs:F3} ms ({taskMs / IterationCount:F6} ms/回)");
            Debug.Log($"  - メモリ: {taskAllocated:N0} bytes ({taskAllocated / IterationCount:N0} bytes/回)");
            Debug.Log("");
            Debug.Log("[UniTask版]");
            Debug.Log($"  - CPU時間: {uniTaskMs:F3} ms ({uniTaskMs / IterationCount:F6} ms/回)");
            Debug.Log($"  - メモリ: {uniTaskAllocated:N0} bytes ({uniTaskAllocated / IterationCount:N0} bytes/回)");
            Debug.Log("");

            if (taskMs > 0 && taskAllocated > 0)
            {
                Debug.Log("[比較]");
                Debug.Log($"  - CPU改善率: {(1 - uniTaskMs / taskMs) * 100:F1}%");
                Debug.Log($"  - メモリ改善率: {(1 - (double)uniTaskAllocated / taskAllocated) * 100:F1}%");
            }

            Assert.Pass("パフォーマンス比較完了。Unity Consoleのログを確認してください。");
        }

        #endregion

        #region Profiler Marker Tests (for Unity Profiler visualization)

        [Test]
        public async Task Profiler_Task_DetailedMarkers()
        {
            Debug.Log("Unity Profilerで Task.* マーカーを確認してください");

            using (TaskTransitionMarker.Auto())
            {
                for (int i = 0; i < 100; i++)
                {
                    await _taskService.TransitionAsync<TaskMockGameScene>();
                }
            }

            using (TaskTerminateMarker.Auto())
            {
                for (int i = 0; i < 100; i++)
                {
                    await _taskService.TerminateLastAsync(clearHistory: true);
                }
            }

            Assert.Pass("Profilerマーカーテスト完了");
        }

        [Test]
        public async Task Profiler_UniTask_DetailedMarkers()
        {
            Debug.Log("Unity Profilerで UniTask.* マーカーを確認してください");

            using (UniTaskTransitionMarker.Auto())
            {
                for (int i = 0; i < 100; i++)
                {
                    await SimulateUniTaskTransition<UniTaskMockGameScene>();
                }
            }

            using (UniTaskTerminateMarker.Auto())
            {
                for (int i = 0; i < 100; i++)
                {
                    await SimulateUniTaskTerminateLast(clearHistory: true);
                }
            }

            Assert.Pass("Profilerマーカーテスト完了");
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