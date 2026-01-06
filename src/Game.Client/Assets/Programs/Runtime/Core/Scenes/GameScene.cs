using System;
using System.Threading.Tasks;
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
        public virtual Task PreInitialize()
        {
            return Task.CompletedTask;
        }

        // アセット(主にこのシーン)をロード
        public virtual Task LoadAsset()
        {
            return Task.CompletedTask;
        }

        // シーンビュー初期化～起動処理
        public virtual Task Startup()
        {
            return Task.CompletedTask;
        }

        // 起動後の処理
        // シーン起動後に演出など
        public virtual Task Ready()
        {
            return Task.CompletedTask;
        }

        public virtual Task Sleep()
        {
            return Task.CompletedTask;
        }

        public virtual Task Restart()
        {
            return Task.CompletedTask;
        }

        // シーンを終了させて破棄する
        public virtual Task Terminate()
        {
            return Task.CompletedTask;
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
        public Func<IGameScene, Task> ArgHandler { get; set; }

        public virtual Task PreInitialize()
        {
            return Task.CompletedTask;
        }

        public virtual Task LoadAsset()
        {
            return Task.CompletedTask;
        }

        public virtual Task Startup()
        {
            return Task.CompletedTask;
        }

        public virtual Task Sleep()
        {
            return Task.CompletedTask;
        }

        public virtual Task Restart()
        {
            return Task.CompletedTask;
        }

        public virtual Task Ready()
        {
            return Task.CompletedTask;
        }

        public virtual Task Terminate()
        {
            return Task.CompletedTask;
        }
    }

    public interface IGameSceneState
    {
        GameSceneState State { get; set; }
    }

    public interface IGameSceneArg<TArg>
    {
        public Task ArgHandle(TArg arg) => Task.CompletedTask;
    }

    public interface IGameSceneArgHandler
    {
        public Func<IGameScene, Task> ArgHandler { get; set; }
    }

    public interface IGameSceneResult
    {
    }

    public interface IGameSceneResult<TResult> : IGameSceneResult
    {
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

        public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;
    }

    public abstract class GameScene<TGameScene, TGameSceneComponent> : GameScene
        where TGameScene : IGameScene
        where TGameSceneComponent : GameSceneComponent
    {
        public TGameSceneComponent SceneComponent { get; protected set; }

        public override Task PreInitialize()
        {
            return base.PreInitialize();
        }

        public override async Task LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        public override Task Startup()
        {
            return base.Startup();
        }

        public override Task Ready()
        {
            return base.Ready();
        }

        public override Task Sleep()
        {
            SceneComponent.Sleep();
            return base.Sleep();
        }

        public override Task Restart()
        {
            SceneComponent.Restart();
            return Ready();
        }

        public override async Task Terminate()
        {
            await UnloadScene();
        }

        protected virtual Task LoadScene()
        {
            return Task.CompletedTask;
        }

        protected virtual Task UnloadScene()
        {
            return Task.CompletedTask;
        }

        protected abstract TGameSceneComponent GetSceneComponent();
    }

    public abstract class GamePrefabScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : IGameScene
        where TGameSceneComponent : GameSceneComponent
    {
        private GameObject _asset;
        private GameObject _instance;

        protected override async Task LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = GameObject.Instantiate(_asset);
            GameSceneHelper.MoveToGameRootScene(_instance);
        }

        protected override Task UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _asset = null;
            }

            return Task.CompletedTask;
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
        where TGameSceneComponent : GameSceneComponent
    {
        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Single;

        private SceneInstance _instance;

        protected override async Task LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected override async Task UnloadScene()
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

        public override async Task LoadAsset()
        {
            await LoadScene();
        }

        public override async Task Terminate()
        {
            await UnloadScene();
        }

        protected virtual async Task LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected virtual async Task UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
        }
    }

    // 任意パラメータを受け取りつつ処理を挟みたいとき
    public interface IGameDialogSceneInitializer<TComponent, TResult>
    {
        public Func<TComponent, IGameSceneResult<TResult>, Task> DialogInitializer { get; set; }
    }

    // 主にダイアログ用(オーバーレイ表示想定)
    // Memo: MonoBehaviourを使う以上、C#では多重継承できないので、個別作成
    public abstract class GameDialogScene<TScene, TComponent, TResult> : GameScene<TScene, TComponent>,
        IGameDialogSceneInitializer<TComponent, TResult>, IGameSceneResult<TResult>
        where TScene : IGameScene
        where TComponent : GameSceneComponent
    {
        public Func<TComponent, IGameSceneResult<TResult>, Task> DialogInitializer { get; set; }

        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        private GameObject _asset;
        private GameObject _instance;

        public override Task PreInitialize()
        {
            return Task.CompletedTask;
        }

        public override async Task LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        public override Task Startup()
        {
            DialogInitializer?.Invoke(SceneComponent, this);
            return Task.CompletedTask;
        }

        public override Task Ready()
        {
            return Task.CompletedTask;
        }

        public override Task Terminate()
        {
            TrySetCanceled();
            return UnloadScene();
        }

        protected override async Task LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = GameObject.Instantiate(_asset);
        }

        protected override Task UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _asset = null;
            }

            return Task.CompletedTask;
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