using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.Constants;
using Game.Core.Enums;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    /// <summary>
    /// GameSceneの遷移挙動を制御するサービス
    /// </summary>
    public partial class GameSceneService : GameService
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        protected internal override bool AllowResidentOnMemory => true;

        private readonly LinkedList<IGameScene> _gameScenes = new();

        private const GameSceneOperations DefaultOperations = GameSceneConstants.DefaultOperations;

        public async UniTask TransitionAsync<TScene>(GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await OperationAsync(operations);

            var gameScene = new TScene();
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
        }

        // 引数つきの画面遷移
        public async UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await OperationAsync(operations);

            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
        }

        // リザルトつきの画面遷移
        public async UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await OperationAsync(operations);

            var gameScene = new TScene();
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
            return await ResultCore(gameScene, tcs);
        }

        // 引数とリザルトつきの画面遷移
        public async UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await OperationAsync(operations);

            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene);
            return await ResultCore(gameScene, tcs);
        }

        // 現在のシーンから見て、前のシーンへ戻る
        public async UniTask TransitionPrevAsync()
        {
            var prevNode = _gameScenes.Last.Previous;
            if (prevNode != null)
            {
                var gameScene = prevNode.Value;
                if (gameScene.State is GameSceneState.Terminate)
                {
                    // 現在のシーンを閉じて履歴を消す
                    await TerminateLastAsync(clearHistory: true);
                    // 履歴から遷移する
                    await TransitionCore(gameScene);
                }
                else if (gameScene.State is GameSceneState.Sleep)
                {
                    // 現在のシーンを閉じて履歴を消す
                    await TerminateLastAsync(clearHistory: true);
                    // スリープ復帰
                    await RestartAsync();
                }
                else if (gameScene.State is GameSceneState.Processing)
                {
                    await TerminateLastAsync(clearHistory: true);
                }
            }
            else
            {
                // なければブラックアウトッ!しないために、ホームやタイトルへ戻る
                // 今はホームがないのでタイトルです
                await TransitionAsync<GameTitleScene>();
            }
        }

        public async UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>(Func<TComponent, IGameSceneResult<TResult>, UniTask> initializer = null)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : GameSceneComponent
        {
            // ダイアログは複数開く事ができる
            // Memo: ダイアログはプロセス中に再度要求されたら閉じる挙動とする(ここは後でダイアログ毎に変えられるようにするかもしれない)
            var type = typeof(TScene);
            if (IsProcessing(type))
            {
                await TerminateAsync(type, clearHistory: true);
                return default;
            }

            // WARN: MonoBehaviourをnewしない方向で実装する必要がある…
            var gameScene = new TScene();
            gameScene.DialogInitializer = initializer;
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.AddLast(gameScene);
            await TransitionCore(gameScene, isDialog: true);
            return await ResultCore(gameScene, tcs);
        }

        // 主に遷移前に現在のシーンに対して何かする
        private async UniTask OperationAsync(GameSceneOperations operations = DefaultOperations)
        {
            // シーン遷移が起こる時はダイアログはすべて閉じる
            await TerminateAllDialogAsync();

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
        /// シーンを起動させる共通処理
        /// </summary>
        private async UniTask TransitionCore(IGameScene gameScene, bool isDialog = false)
        {
            gameScene.State = GameSceneState.Processing;

            if (gameScene.ArgHandler != null)
                await gameScene.ArgHandler.Invoke(gameScene);

            if (!isDialog) await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameScene.TransitionEnter, true);
            await gameScene.PreInitialize();
            await gameScene.LoadAsset();
            await gameScene.Startup();
            if (!isDialog) await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.GameScene.TransitionFinish, true);
            await gameScene.Ready();
        }

        private async UniTask<TResult> ResultCore<TResult>(IGameScene gameScene, UniTaskCompletionSource<TResult> tcs)
        {
            if (tcs == null) return default;

            try
            {
                var result = await tcs.Task;
                await TerminateAsync(gameScene, clearHistory: true); // リザルトがセットされ、プロセスが終わったら閉じる, 遷移履歴も消す
                return result;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされたら閉じるようにしておく
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

        private UniTask SleepAsync()
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

            return UniTask.CompletedTask;
        }

        private UniTask RestartAsync()
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

            return UniTask.CompletedTask;
        }

        private async UniTask TerminateAsync(IGameScene gameScene, bool clearHistory = false)
        {
            var node = _gameScenes.FindLast(gameScene);
            if (node != null)
            {
                await TerminateCore(node.Value);

                if (clearHistory) _gameScenes.Remove(node);
            }
        }

        public async UniTask TerminateAsync(Type type, bool clearHistory = false)
        {
            var gameScene = _gameScenes.LastOrDefault(x => x.GetType() == type);
            if (gameScene != null)
            {
                await TerminateCore(gameScene);

                if (clearHistory) _gameScenes.Remove(gameScene);
            }
        }

        // 最後に開いたものを閉じる
        public async UniTask TerminateLastAsync(bool clearHistory = false)
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

        private async UniTask TerminateAllDialogAsync()
        {
            foreach (var gameScene in _gameScenes.Reverse())
            {
                // リザルト持ちのシーンもダイアログとする
                if (gameScene is IGameSceneResult)
                {
                    await TerminateAsync(gameScene, clearHistory: true);
                }
            }
        }

        private async UniTask TerminateAllAsync()
        {
            foreach (var gameScene in _gameScenes.Reverse())
            {
                await TerminateCore(gameScene);
            }

            _gameScenes.Clear();
        }

        private async UniTask TerminateCore(IGameScene gameScene)
        {
            if (gameScene != null)
            {
                gameScene.State = GameSceneState.Terminate;
                await gameScene.Terminate();
            }
        }

        #region UnityScene

        private readonly List<SceneInstance> _unityScenes = new();

        public async UniTask<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            var sceneInstance = await AssetService.LoadSceneAsync(sceneName, loadSceneMode, activateOnLoad);
            if (sceneInstance.Scene.IsValid())
            {
                _unityScenes.Add(sceneInstance);
            }

            return sceneInstance;
        }

        public async UniTask UnloadUnitySceneAsync(SceneInstance sceneInstance)
        {
            await AssetService.UnloadSceneAsync(sceneInstance);

            if (_unityScenes.Contains(sceneInstance))
                _unityScenes.Remove(sceneInstance);
        }

        public async UniTask UnloadUnitySceneAllAsync()
        {
            foreach (var sceneInstance in _unityScenes)
            {
                await AssetService.UnloadSceneAsync(sceneInstance);
            }

            _unityScenes.Clear();
        }

        #endregion
    }
}