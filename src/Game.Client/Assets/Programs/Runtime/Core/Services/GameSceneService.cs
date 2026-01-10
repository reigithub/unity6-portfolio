using System;
using System.Collections.Generic;
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
    public partial class GameSceneService : IGameSceneService
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private readonly List<IGameScene> _gameScenes = new(16);

        private const GameSceneOperations DefaultOperations = GameSceneConstants.DefaultOperations;

        public async UniTask TransitionAsync<TScene>(GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
        }

        // 引数つきの画面遷移
        public async UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            CreateArgHandler(gameScene, arg);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
        }

        // リザルトつきの画面遷移
        public async UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = DefaultOperations)
            where TScene : IGameScene, new()
        {
            await CurrentSceneOperationAsync(operations);

            var gameScene = new TScene();
            var tcs = CreateResultTcs<TResult>(gameScene);
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene);
            return await ResultAsync(gameScene, tcs);
        }

        // 引数とリザルトつきの画面遷移
        public async UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = DefaultOperations)
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

        // 現在のシーンから見て、前のシーンへ戻る
        public async UniTask TransitionPrevAsync()
        {
            if (_gameScenes.Count >= 2)
            {
                var prevScene = _gameScenes[^2];
                if (prevScene.State is GameSceneState.Terminate)
                {
                    // 現在のシーンを閉じて履歴を消す
                    await TerminateLastAsync(clearHistory: true);
                    // 履歴から遷移する
                    await TransitionCore(prevScene);
                }
                else if (prevScene.State is GameSceneState.Sleep)
                {
                    // 現在のシーンを閉じて履歴を消す
                    await TerminateLastAsync(clearHistory: true);
                    // スリープ復帰
                    await RestartAsync();
                }
                else if (prevScene.State is GameSceneState.Processing)
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
            where TComponent : IGameSceneComponent
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
            _gameScenes.Add(gameScene);
            await TransitionCore(gameScene, isDialog: true);
            return await ResultAsync(gameScene, tcs);
        }

        // 主に遷移前に現在のシーンに対して何かする
        private async UniTask CurrentSceneOperationAsync(GameSceneOperations operations = DefaultOperations)
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

        private async UniTask<TResult> ResultAsync<TResult>(IGameScene gameScene, UniTaskCompletionSource<TResult> tcs)
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
            if (_gameScenes.Count == 0) return false;

            var gameScene = _gameScenes[^1];
            return gameScene.GetType() == type && gameScene.State is GameSceneState.Processing;
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

        private async UniTask TerminateAsync(IGameScene gameScene, bool clearHistory = false)
        {
            var index = _gameScenes.LastIndexOf(gameScene);
            if (index >= 0)
            {
                await TerminateCore(gameScene);

                if (clearHistory) _gameScenes.RemoveAt(index);
            }
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

        // 最後に開いたものを閉じる
        public async UniTask TerminateLastAsync(bool clearHistory = false)
        {
            if (_gameScenes.Count == 0) return;

            var lastIndex = _gameScenes.Count - 1;
            var gameScene = _gameScenes[lastIndex];

            await TerminateCore(gameScene);

            if (clearHistory) _gameScenes.RemoveAt(lastIndex);
        }

        private async UniTask TerminateAllDialogAsync()
        {
            // 逆順で走査（削除時にインデックスがずれないように）
            for (int i = _gameScenes.Count - 1; i >= 0; i--)
            {
                var gameScene = _gameScenes[i];
                // リザルト持ちのシーンもダイアログとする
                if (gameScene is IGameSceneResult)
                {
                    await TerminateCore(gameScene);
                    _gameScenes.RemoveAt(i);
                }
            }
        }

        private async UniTask TerminateAllAsync()
        {
            // 逆順で終了処理
            for (int i = _gameScenes.Count - 1; i >= 0; i--)
            {
                await TerminateCore(_gameScenes[i]);
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

        /// <summary>
        /// 指定した型のシーンを末尾から検索してインデックスを返す
        /// </summary>
        private int FindLastIndexByType(Type type)
        {
            for (int i = _gameScenes.Count - 1; i >= 0; i--)
            {
                if (_gameScenes[i].GetType() == type)
                    return i;
            }

            return -1;
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