using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    public class GameSceneService<TGameScene> : GameService
        where TGameScene : GameScene
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        protected AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        protected GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        protected internal override bool AllowResidentOnMemory => true;
    }

    /// <summary>
    /// GameSceneの遷移挙動を制御するサービス
    /// </summary>
    public partial class GameSceneService : GameSceneService<GameScene>
    {
        // 1.履歴を持って、一つ前のシーンへ戻れるように
        // 2.現在シーンをスリープさせて、次のシーンを表示する処理
        // 3.ダイアログ用のオーバーレイ表示（→完了）
        // 4.マルチ解像度対応（いずれどこかで…）
        private readonly List<(Type type, IGameScene gameScene)> _gameScenes = new();
        private readonly List<SceneInstance> _unityScenes = new();

        public async Task TransitionAsync<TScene>()
            where TScene : IGameScene, new()
        {
            // Memo: まだスリープとかクロスフェードなどの概念を入れていないので、一旦全て終了させてから開く
            await TerminateAllAsync();

            var gameScene = new TScene();
            await TransitionCore(gameScene);
            _gameScenes.Add((typeof(TScene), gameScene));
        }

        // 引数とモデルクラスつきの画面遷移
        public async Task TransitionAsync<TScene, TModel, TArg>(TArg arg)
            where TScene : IGameScene, IGameSceneModel<TModel>, IGameSceneArg<TArg>, new()
            where TModel : class, new()
        {
            await TerminateAllAsync();

            var sceneType = typeof(TScene);
            var gameScene = new TScene();
            gameScene.SceneModel = new TModel();
            await gameScene.PreInitialize(arg);
            await TransitionCore(gameScene);
            _gameScenes.Add((sceneType, gameScene));
        }

        // 引数つきの画面遷移
        public async Task TransitionAsync<TScene, TArg>(TArg arg)
            where TScene : IGameScene, IGameSceneArg<TArg>, new()
        {
            await TerminateAllAsync();

            var gameScene = new TScene();
            await gameScene.PreInitialize(arg);
            await TransitionCore(gameScene);
            _gameScenes.Add((typeof(TScene), gameScene));
        }

        // リザルトつきの画面遷移
        public async Task<TResult> TransitionAsync<TScene, TResult>()
            where TScene : IGameScene, IGameSceneResult<TResult>, new()
        {
            await TerminateAllAsync();

            var gameScene = new TScene();
            var tcs = gameScene.ResultTcs = new UniTaskCompletionSource<TResult>();

            await TransitionCore(gameScene);
            _gameScenes.Add((typeof(TScene), gameScene));

            // Memo: リザルト周りは処理をまとめたい…
            try
            {
                var result = await tcs.Task;
                await TerminateAsync<TScene>();
                return result;
            }
            catch (OperationCanceledException)
            {
                await TerminateAsync<TScene>();
            }

            return default;
        }

        // 引数とリザルトつきの画面遷移
        public async Task<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg)
            where TScene : IGameScene, IGameSceneArg<TArg>, IGameSceneResult<TResult>, new()
        {
            await TerminateAllAsync();

            var gameScene = new TScene();
            await gameScene.PreInitialize(arg);
            var tcs = gameScene.ResultTcs = new UniTaskCompletionSource<TResult>();

            await TransitionCore(gameScene);
            _gameScenes.Add((typeof(TScene), gameScene));

            try
            {
                var result = await tcs.Task;
                await TerminateAsync<TScene>();
                return result;
            }
            catch (OperationCanceledException)
            {
                await TerminateAsync<TScene>();
            }

            return default;
        }

        public async Task<TResult> TransitionDialogAsync<TScene, TComponent, TResult>(Func<TScene, TComponent, Task> startup = null)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : GameSceneComponent
        {
            // Memo: ダイアログはプロセス中に再度要求されたら閉じる挙動とする(ここは後でダイアログ毎に変えられるようにするかもしれない)
            if (IsProcessing<TScene>())
            {
                await TerminateAsync<TScene>();
                return default;
            }

            // WARN: MonoBehaviourをnewしない方向で実装する必要がある…
            var gameScene = new TScene();
            gameScene.Scene = gameScene; // コンポーネント側からダイアログ操作などを可能にするために、具象化クラスをベースクラスへ入れたい…（本当はMVCならモデルを渡すべきか）
            gameScene.StartupFilter = startup;
            var tcs = gameScene.ResultTcs = new UniTaskCompletionSource<TResult>();
            await TransitionCore(gameScene);
            _gameScenes.Add((typeof(TScene), gameScene));

            try
            {
                var result = await tcs.Task;
                await TerminateAsync<TScene>(); // リザルトがセットされ、プロセスが終わったら閉じる
                return result;
            }
            catch (OperationCanceledException)
            {
                // Debug.LogError($"{e.Message}");
                await TerminateAsync<TScene>(); // キャンセルされたら閉じるようにしておく
            }

            return default;
        }

        /// <summary>
        /// シーンを起動させる共通処理
        /// </summary>
        private async Task TransitionCore(IGameScene gameScene)
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.GameScene.TransitionEnter, true);
            await gameScene.LoadAsset();
            await gameScene.PreInitialize();
            await gameScene.Startup();
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.GameScene.TransitionFinish, true);
            await gameScene.Ready();
        }

        public bool IsProcessing<TScene>()
            where TScene : IGameScene
        {
            // Memo: デフォルトでプロセス実行中の監視用タスクを持たせるか検討（ダイアログのキャンセル機構を参考に）
            var type = typeof(TScene);
            return _gameScenes.Any(x => x.type == type);
        }

        public async Task<bool> TerminateAsync<TScene>()
            where TScene : IGameScene
        {
            var type = typeof(TScene);
            var target = _gameScenes.LastOrDefault(x => x.type == type);
            if (target.type != null)
            {
                await target.gameScene.Terminate();
                _gameScenes.Remove(target);
                return true;
            }

            return false;
        }

        private async Task TerminateAllAsync()
        {
            // Memo: インスタンスを抹殺するので、逆から閉じないとオペレーションエラーになるヨ
            foreach (var (_, scene) in Enumerable.Reverse(_gameScenes))
            {
                await scene.Terminate();
            }

            _gameScenes.Clear();
        }

        public async Task<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            var sceneInstance = await AssetService.LoadSceneAsync(sceneName, loadSceneMode, activateOnLoad);
            if (sceneInstance.Scene.IsValid())
            {
                _unityScenes.Add(sceneInstance);
            }

            return sceneInstance;
        }

        public async Task UnloadUnitySceneAsync(SceneInstance sceneInstance)
        {
            await AssetService.UnloadSceneAsync(sceneInstance);

            if (_unityScenes.Contains(sceneInstance))
                _unityScenes.Remove(sceneInstance);
        }

        public async Task UnloadUnitySceneAllAsync()
        {
            foreach (var sceneInstance in _unityScenes)
            {
                await AssetService.UnloadSceneAsync(sceneInstance);
            }

            _unityScenes.Clear();
        }
    }
}