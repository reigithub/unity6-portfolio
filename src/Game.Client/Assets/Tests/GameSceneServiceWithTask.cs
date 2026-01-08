using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Scenes;

namespace Game.Tests
{
    /// <summary>
    /// パフォーマンス比較用のTask版GameSceneService
    /// UniTask版との比較のため、意図的にTaskを使用しています
    /// </summary>
    public class GameSceneServiceWithTask
    {
        private readonly LinkedList<ITaskGameScene> _gameScenes = new();

        public async Task TransitionAsync<TScene>()
            where TScene : ITaskGameScene, new()
        {
            var gameScene = new TScene();
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
        }

        public async Task TransitionAsync<TScene, TArg>(TArg arg)
            where TScene : ITaskGameScene, new()
        {
            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
        }

        public async Task<TResult> TransitionAsync<TScene, TResult>()
            where TScene : ITaskGameScene, new()
        {
            var gameScene = new TScene();
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
            return await ResultCore(gameScene, tcs);
        }

        public async Task<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg)
            where TScene : ITaskGameScene, new()
        {
            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
            return await ResultCore(gameScene, tcs);
        }

        public async Task TransitionPrevAsync()
        {
            var prevNode = _gameScenes.Last?.Previous;
            if (prevNode != null)
            {
                var gameScene = prevNode.Value;
                if (gameScene.State is GameSceneState.Terminate)
                {
                    await TerminateLastAsync(clearHistory: true);
                    await TransitionCore(gameScene);
                }
                else if (gameScene.State is GameSceneState.Sleep)
                {
                    await TerminateLastAsync(clearHistory: true);
                    await RestartAsync();
                }
                else if (gameScene.State is GameSceneState.Processing)
                {
                    await TerminateLastAsync(clearHistory: true);
                }
            }
        }

        private void CreateArgHandler<TArg>(ITaskGameScene gameScene, TArg arg)
        {
            if (gameScene is ITaskGameSceneArgHandler handler)
            {
                handler.ArgHandler = scene =>
                {
                    if (scene is ITaskGameSceneArg<TArg> gameSceneArg)
                        return gameSceneArg.ArgHandle(arg);

                    return Task.CompletedTask;
                };
            }
        }

        private TaskCompletionSource<TResult> CreateResultTcs<TResult>(ITaskGameScene gameScene)
        {
            if (gameScene is ITaskGameSceneResult<TResult> result)
            {
                return result.ResultTcs = new TaskCompletionSource<TResult>();
            }

            return null;
        }

        private async Task TransitionCore(ITaskGameScene gameScene)
        {
            gameScene.State = GameSceneState.Processing;

            if (gameScene.ArgHandler != null)
                await gameScene.ArgHandler.Invoke(gameScene);

            await gameScene.PreInitialize();
            await gameScene.LoadAsset();
            await gameScene.Startup();
            await gameScene.Ready();
        }

        private async Task<TResult> ResultCore<TResult>(ITaskGameScene gameScene, TaskCompletionSource<TResult> tcs)
        {
            if (tcs == null) return default;

            try
            {
                var result = await tcs.Task;
                await TerminateAsync(gameScene, clearHistory: true);
                return result;
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }

            return default;
        }

        public bool IsProcessing(Type type)
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                return gameScene.GetType() == type && gameScene.State is GameSceneState.Processing;
            }

            return false;
        }

        private Task SleepAsync()
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    gameScene.State = GameSceneState.Sleep;
                    return gameScene.Sleep();
                }
            }

            return Task.CompletedTask;
        }

        private Task RestartAsync()
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    gameScene.State = GameSceneState.Processing;
                    return gameScene.Restart();
                }
            }

            return Task.CompletedTask;
        }

        private async Task TerminateAsync(ITaskGameScene gameScene, bool clearHistory = false)
        {
            var node = _gameScenes.FindLast(gameScene);
            if (node != null)
            {
                await TerminateCore(node.Value);

                if (clearHistory) _gameScenes.Remove(node);
            }
        }

        public async Task TerminateAsync(Type type, bool clearHistory = false)
        {
            var gameScene = _gameScenes.LastOrDefault(x => x.GetType() == type);
            if (gameScene != null)
            {
                await TerminateCore(gameScene);

                if (clearHistory) _gameScenes.Remove(gameScene);
            }
        }

        public async Task TerminateLastAsync(bool clearHistory = false)
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    await TerminateAsync(gameScene, clearHistory);
                }
            }
        }

        private async Task TerminateCore(ITaskGameScene gameScene)
        {
            if (gameScene != null)
            {
                gameScene.State = GameSceneState.Terminate;
                await gameScene.Terminate();
            }
        }

        public void Clear()
        {
            _gameScenes.Clear();
        }
    }

    #region Task版インターフェース

    public interface ITaskGameScene : ITaskGameSceneState, ITaskGameSceneArgHandler
    {
        Task PreInitialize();
        Task LoadAsset();
        Task Startup();
        Task Ready();
        Task Sleep();
        Task Restart();
        Task Terminate();
    }

    public interface ITaskGameSceneState
    {
        GameSceneState State { get; set; }
    }

    public interface ITaskGameSceneArg<in TArg>
    {
        Task ArgHandle(TArg arg);
    }

    public interface ITaskGameSceneArgHandler
    {
        Func<ITaskGameScene, Task> ArgHandler { get; set; }
    }

    public interface ITaskGameSceneResult<TResult>
    {
        TaskCompletionSource<TResult> ResultTcs { get; set; }

        bool TrySetResult(TResult result)
        {
            return ResultTcs?.TrySetResult(result) ?? false;
        }

        bool TrySetCanceled()
        {
            return ResultTcs?.TrySetCanceled() ?? false;
        }

        bool TrySetException(Exception e)
        {
            return ResultTcs?.TrySetException(e) ?? false;
        }
    }

    #endregion

    #region Task版モッククラス

    public class TaskMockGameScene : ITaskGameScene
    {
        public GameSceneState State { get; set; }
        public Func<ITaskGameScene, Task> ArgHandler { get; set; }

        public int PreInitializeCount { get; private set; }
        public int LoadAssetCount { get; private set; }
        public int StartupCount { get; private set; }
        public int ReadyCount { get; private set; }
        public int SleepCount { get; private set; }
        public int RestartCount { get; private set; }
        public int TerminateCount { get; private set; }

        public Task PreInitialize() { PreInitializeCount++; return Task.CompletedTask; }
        public Task LoadAsset() { LoadAssetCount++; return Task.CompletedTask; }
        public Task Startup() { StartupCount++; return Task.CompletedTask; }
        public Task Ready() { ReadyCount++; return Task.CompletedTask; }
        public Task Sleep() { SleepCount++; return Task.CompletedTask; }
        public Task Restart() { RestartCount++; return Task.CompletedTask; }
        public Task Terminate() { TerminateCount++; return Task.CompletedTask; }
    }

    public class TaskMockGameSceneWithArg<TArg> : ITaskGameScene, ITaskGameSceneArg<TArg>
    {
        public GameSceneState State { get; set; }
        public Func<ITaskGameScene, Task> ArgHandler { get; set; }
        public TArg ReceivedArg { get; private set; }

        public Task PreInitialize() => Task.CompletedTask;
        public Task LoadAsset() => Task.CompletedTask;
        public Task Startup() => Task.CompletedTask;
        public Task Ready() => Task.CompletedTask;
        public Task Sleep() => Task.CompletedTask;
        public Task Restart() => Task.CompletedTask;
        public Task Terminate() => Task.CompletedTask;

        public Task ArgHandle(TArg arg)
        {
            ReceivedArg = arg;
            return Task.CompletedTask;
        }
    }

    public class TaskMockGameSceneWithResult<TResult> : ITaskGameScene, ITaskGameSceneResult<TResult>
    {
        public GameSceneState State { get; set; }
        public Func<ITaskGameScene, Task> ArgHandler { get; set; }
        public TaskCompletionSource<TResult> ResultTcs { get; set; }

        public Task PreInitialize() => Task.CompletedTask;
        public Task LoadAsset() => Task.CompletedTask;
        public Task Startup() => Task.CompletedTask;
        public Task Ready() => Task.CompletedTask;
        public Task Sleep() => Task.CompletedTask;
        public Task Restart() => Task.CompletedTask;
        public Task Terminate() => Task.CompletedTask;
    }

    #endregion
}
