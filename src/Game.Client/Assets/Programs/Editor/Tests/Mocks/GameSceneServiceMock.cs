using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Scenes;
using Game.Core.Services;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Editor.Tests
{
    /// <summary>
    /// テスト用のGameSceneServiceモック
    /// MessageBroker等の外部依存を排除したシンプルな実装
    /// </summary>
    public class GameSceneServiceMock : IGameSceneService
    {
        private readonly List<IGameScene> _gameScenes = new(16);

        #region Test Verification Properties

        /// <summary>
        /// 現在のシーン数（テスト検証用）
        /// </summary>
        public int SceneCount => _gameScenes.Count;

        /// <summary>
        /// シーンリストへの読み取り専用アクセス（テスト検証用）
        /// </summary>
        public IReadOnlyList<IGameScene> Scenes => _gameScenes;

        /// <summary>
        /// 最後のシーンを取得（テスト検証用）
        /// </summary>
        public IGameScene LastScene => _gameScenes.Count > 0 ? _gameScenes[^1] : null;

        /// <summary>
        /// 指定インデックスのシーンを取得（テスト検証用）
        /// </summary>
        public IGameScene GetScene(int index) => index >= 0 && index < _gameScenes.Count ? _gameScenes[index] : null;

        /// <summary>
        /// 内部状態をクリア
        /// </summary>
        public void Clear() => _gameScenes.Clear();

        /// <summary>
        /// シーンを直接追加（テストセットアップ用）
        /// </summary>
        public void AddScene(IGameScene scene) => _gameScenes.Add(scene);

        /// <summary>
        /// シーン操作を実行（テスト用）
        /// </summary>
        public UniTask ExecuteOperationAsync(GameSceneOperations operations) => CurrentSceneOperationAsync(operations);

        /// <summary>
        /// 追加済みシーンに対してTransitionCoreを実行（テスト用）
        /// </summary>
        public UniTask ExecuteTransitionCoreAsync(IGameScene gameScene) => TransitionCore(gameScene);

        /// <summary>
        /// ArgHandlerをセットアップ（テスト用）
        /// </summary>
        public void SetupArgHandler<TArg>(IGameScene gameScene, TArg arg) => CreateArgHandler(gameScene, arg);

        /// <summary>
        /// ResultTcsをセットアップ（テスト用）
        /// </summary>
        public UniTaskCompletionSource<TResult> SetupResultTcs<TResult>(IGameScene gameScene) => CreateResultTcs<TResult>(gameScene);

        /// <summary>
        /// 全ダイアログを終了（テスト用）
        /// </summary>
        public async UniTask TerminateAllDialogAsync()
        {
            for (int i = _gameScenes.Count - 1; i >= 0; i--)
            {
                var gameScene = _gameScenes[i];
                if (gameScene is IGameSceneResult)
                {
                    await TerminateCore(gameScene);
                    _gameScenes.RemoveAt(i);
                }
            }
        }

        #endregion

        public async UniTask TransitionAsync<TScene>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
        }

        public async UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
        }

        public async UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
            return await ResultAsync(gameScene, tcs);
        }

        public async UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
            return await ResultAsync(gameScene, tcs);
        }

        public async UniTask TransitionPrevAsync()
        {
            if (_gameScenes.Count >= 2)
            {
                var prevScene = _gameScenes[^2];
                if (prevScene.State is GameSceneState.Terminate)
                {
                    await TerminateLastAsync(clearHistory: true);
                    await TransitionCore(prevScene);
                }
                else if (prevScene.State is GameSceneState.Sleep)
                {
                    await TerminateLastAsync(clearHistory: true);
                    await RestartAsync();
                }
                else if (prevScene.State is GameSceneState.Processing)
                {
                    await TerminateLastAsync(clearHistory: true);
                }
            }
            // モックではフォールバック遷移は行わない
        }

        public async UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>(Func<TComponent, IGameSceneResult<TResult>, UniTask> initializer = null)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : IGameSceneComponent
        {
            var type = typeof(TScene);
            if (IsProcessing(type))
            {
                await TerminateAsync(type, clearHistory: true);
                return default;
            }

            var gameScene = new TScene();
            gameScene.DialogInitializer = initializer;
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
            return await ResultAsync(gameScene, tcs);
        }

        public bool IsProcessing(Type type)
        {
            if (_gameScenes.Count == 0) return false;

            var gameScene = _gameScenes[^1];
            return gameScene.GetType() == type && gameScene.State is GameSceneState.Processing;
        }

        public async UniTask TerminateAsync(Type type, bool clearHistory = false)
        {
            var index = FindLastIndexByType(type);
            if (index >= 0)
            {
                var gameScene = _gameScenes[index];
                await TerminateCore(gameScene);

                if (clearHistory) _gameScenes.RemoveAt(index);
            }
        }

        public async UniTask TerminateLastAsync(bool clearHistory = false)
        {
            if (_gameScenes.Count == 0) return;

            var lastIndex = _gameScenes.Count - 1;
            var gameScene = _gameScenes[lastIndex];

            await TerminateCore(gameScene);

            if (clearHistory) _gameScenes.RemoveAt(lastIndex);
        }

        // UnitySceneメソッドはモックのため空実装
        public UniTask<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            return UniTask.FromResult(default(SceneInstance));
        }

        public UniTask UnloadUnitySceneAsync(SceneInstance sceneInstance)
        {
            return UniTask.CompletedTask;
        }

        public UniTask UnloadUnitySceneAllAsync()
        {
            return UniTask.CompletedTask;
        }

        #region Private Methods

        private async UniTask CurrentSceneOperationAsync(GameSceneOperations operations)
        {
            if (operations.HasFlag(GameSceneOperations.CurrentSceneSleep))
            {
                await SleepAsync();
            }
            else if (operations.HasFlag(GameSceneOperations.CurrentSceneRestart))
            {
                await RestartAsync();
            }
            else if (operations.HasFlag(GameSceneOperations.CurrentSceneTerminate))
            {
                bool clearHistory = operations.HasFlag(GameSceneOperations.CurrentSceneClearHistory);
                await TerminateLastAsync(clearHistory);
            }
        }

        private void CreateArgHandler<TArg>(IGameScene gameScene, TArg arg)
        {
            if (gameScene is IGameSceneArgHandler handler)
            {
                handler.ArgHandler = scene =>
                {
                    if (scene is IGameSceneArg<TArg> gameSceneArg)
                        return gameSceneArg.ArgHandle(arg);

                    return UniTask.CompletedTask;
                };
            }
        }

        private UniTaskCompletionSource<TResult> CreateResultTcs<TResult>(IGameScene gameScene)
        {
            if (gameScene is IGameSceneResult<TResult> result)
            {
                return result.ResultTcs = new UniTaskCompletionSource<TResult>();
            }

            return null;
        }

        /// <summary>
        /// シーンを起動させる共通処理（MessageBroker呼び出しなし）
        /// </summary>
        private async UniTask TransitionCore(IGameScene gameScene)
        {
            gameScene.State = GameSceneState.Processing;

            if (gameScene.ArgHandler != null)
                await gameScene.ArgHandler.Invoke(gameScene);

            await gameScene.PreInitialize();
            await gameScene.LoadAsset();
            await gameScene.Startup();
            await gameScene.Ready();
        }

        private async UniTask<TResult> ResultAsync<TResult>(IGameScene gameScene, UniTaskCompletionSource<TResult> tcs)
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

        private async UniTask TerminateAsync(IGameScene gameScene, bool clearHistory = false)
        {
            var index = _gameScenes.LastIndexOf(gameScene);
            if (index >= 0)
            {
                await TerminateCore(gameScene);

                if (clearHistory) _gameScenes.RemoveAt(index);
            }
        }

        private UniTask SleepAsync()
        {
            if (_gameScenes.Count == 0) return UniTask.CompletedTask;

            var gameScene = _gameScenes[^1];
            gameScene.State = GameSceneState.Sleep;
            return gameScene.Sleep();
        }

        private UniTask RestartAsync()
        {
            if (_gameScenes.Count == 0) return UniTask.CompletedTask;

            var gameScene = _gameScenes[^1];
            gameScene.State = GameSceneState.Processing;
            return gameScene.Restart();
        }

        private async UniTask TerminateCore(IGameScene gameScene)
        {
            if (gameScene != null)
            {
                gameScene.State = GameSceneState.Terminate;
                await gameScene.Terminate();
            }
        }

        private int FindLastIndexByType(Type type)
        {
            for (int i = _gameScenes.Count - 1; i >= 0; i--)
            {
                if (_gameScenes[i].GetType() == type)
                    return i;
            }

            return -1;
        }

        #endregion
    }
}
