using System;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MasterData;
using Game.Core.MessagePipe;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Scenes
{
    public interface IGameScene : IGameSceneState, IGameSceneArgHandler
    {
        // 事前初期化処理
        // サーバー通信, モデルクラスの初期化など
        public UniTask PreInitialize()
        {
            return UniTask.CompletedTask;
        }

        // アセット(主にこのシーン)をロード
        public UniTask LoadAsset()
        {
            return UniTask.CompletedTask;
        }

        // シーンビュー初期化～起動処理
        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        // 起動後の処理
        // シーン起動後に演出など
        public UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        // シーンを終了させて破棄する
        public UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }
    }

    public abstract class GameScene : IGameScene
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        protected AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<AudioService> _audioService;
        protected AudioService AudioService => _audioService.Reference;

        private GameServiceReference<GameSceneService> _sceneService;
        protected GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MasterDataService> _masterDataService;
        protected MasterDataService MasterDataService => _masterDataService.Reference;
        protected MemoryDatabase MemoryDatabase => MasterDataService.MemoryDatabase;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        protected MessageBrokerService MessageBrokerService => _messageBrokerService.Reference;
        protected GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        protected abstract string AssetPathOrAddress { get; }

        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }

        public virtual UniTask PreInitialize()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask LoadAsset()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }
    }

    public interface IGameSceneState
    {
        GameSceneState State { get; set; }
    }

    public interface IGameSceneArg<in TArg>
    {
        public UniTask ArgHandle(TArg arg);
    }

    public interface IGameSceneArgHandler
    {
        public Func<IGameScene, UniTask> ArgHandler { get; set; }
    }

    public interface IGameSceneResult
    {
    }

    public interface IGameSceneResult<TResult> : IGameSceneResult
    {
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

        public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;

        public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;
    }

    public abstract class GameScene<TGameScene, TGameSceneComponent> : GameScene
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        protected TGameSceneComponent SceneComponent { get; set; }

        public override UniTask PreInitialize()
        {
            return base.PreInitialize();
        }

        public override async UniTask LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        public override UniTask Startup()
        {
            return base.Startup();
        }

        public override UniTask Ready()
        {
            return base.Ready();
        }

        public override UniTask Sleep()
        {
            SceneComponent.Sleep();
            return base.Sleep();
        }

        public override UniTask Restart()
        {
            SceneComponent.Restart();
            return Ready();
        }

        public override async UniTask Terminate()
        {
            await UnloadScene();
        }

        protected virtual UniTask LoadScene()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask UnloadScene()
        {
            return UniTask.CompletedTask;
        }

        protected abstract TGameSceneComponent GetSceneComponent();
    }

    public abstract class GamePrefabScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        private GameObject _asset;
        private GameObject _instance;

        protected override async UniTask LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = UnityEngine.Object.Instantiate(_asset);
            GameSceneHelper.MoveToGameRootScene(_instance);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _asset = null;
            }

            return UniTask.CompletedTask;
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance);
        }
    }

    // コンポーネント付きのUnityScene
    // Memo: 多分使わない
    // 基本的にPrefabSceneで賄えるのと、PrefabSceneを使う際にGameRootSceneを戻してあげないといけないので面倒
    public abstract class GameUnityScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Single;

        private SceneInstance _instance;

        protected override async UniTask LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected override async UniTask UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance.Scene);
        }
    }

    // コンポーネントなしのピュアなUnityScene
    // Memo: UnityScene毎にクラス作成するはめになるのでナシの方向（基本的にPrefabSceneのついでに、読み込む形で良い）
    // 新しいフィールド作成毎にコード追加が発生するため、チーム開発には向いてないかも、ということで
    public abstract class GameUnityScene : GameScene
    {
        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Additive;

        private SceneInstance _instance;

        public override async UniTask LoadAsset()
        {
            await LoadScene();
        }

        public override async UniTask Terminate()
        {
            await UnloadScene();
        }

        protected virtual async UniTask LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected virtual async UniTask UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
        }
    }

    // 任意パラメータを受け取りつつ処理を挟みたいとき
    public interface IGameDialogSceneInitializer<TComponent, TResult>
    {
        public Func<TComponent, IGameSceneResult<TResult>, UniTask> DialogInitializer { get; set; }
    }

    // 主にダイアログ用(オーバーレイ表示想定)
    public abstract class GameDialogScene<TScene, TComponent, TResult> : GameScene<TScene, TComponent>,
        IGameDialogSceneInitializer<TComponent, TResult>, IGameSceneResult<TResult>
        where TScene : IGameScene
        where TComponent : IGameSceneComponent
    {
        public Func<TComponent, IGameSceneResult<TResult>, UniTask> DialogInitializer { get; set; }

        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        private GameObject _asset;
        private GameObject _instance;

        public override UniTask PreInitialize()
        {
            return UniTask.CompletedTask;
        }

        public override async UniTask LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        public override UniTask Startup()
        {
            if (DialogInitializer != null)
            {
                return DialogInitializer.Invoke(SceneComponent, this);
            }

            return UniTask.CompletedTask;
        }

        public override UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public override UniTask Terminate()
        {
            TrySetCanceled();
            return UnloadScene();
        }

        protected override async UniTask LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = UnityEngine.Object.Instantiate(_asset);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _asset = null;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// リザルトをセットしてダイアログを閉じる
        /// </summary>
        public bool TrySetResult(TResult result)
        {
            return ResultTcs?.TrySetResult(result) ?? false;
        }

        /// <summary>
        /// ダイアログをキャンセルして閉じる
        /// </summary>
        public bool TrySetCanceled()
        {
            return ResultTcs?.TrySetCanceled() ?? false;
        }

        protected override TComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TComponent>(_instance);
        }
    }
}