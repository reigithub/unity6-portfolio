using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Scenes
{
    public interface IGameScene
    {
        // アセット(主にこのシーン)をロード
        public virtual Task<GameObject> LoadAsset()
        {
            return Task.FromResult<GameObject>(null);
        }

        // 事前初期化処理
        // サーバー通信など…???
        public virtual Task PreInitialize()
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

        private GameServiceReference<GameSceneService> _sceneService;
        protected GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        protected MessageBrokerService MessageBrokerService => _messageBrokerService.Reference;
        protected GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        protected abstract string AssetPathOrAddress { get; }

        public virtual Task<GameObject> LoadAsset()
        {
            return Task.FromResult<GameObject>(null);
        }

        public virtual Task PreInitialize()
        {
            return Task.CompletedTask;
        }

        public virtual Task Startup()
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

    // Memo: Arg, Resultと同じく任意でつけられるという事にする
    public interface IGameSceneModel<TSceneModel>
        where TSceneModel : class, new()
    {
        public TSceneModel SceneModel { get; set; }
    }

    public interface IGameSceneArg<TArg>
    {
        public Task PreInitialize(TArg arg) => Task.CompletedTask;
    }

    public interface IGameSceneResult<TResult>
    {
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }
    }

    // 任意パラメータを受け取りつつ処理を挟みたいとき
    // 主にダイアログ
    // プロセス毎に分けるか要検討
    public interface IGameSceneStartupFilter<TScene, TSceneComponent>
    {
        public Func<TScene, TSceneComponent, Task> StartupFilter { get; set; }
    }

    public abstract class GameScene<TGameScene, TGameSceneComponent> : GameScene
        where TGameScene : IGameScene
        where TGameSceneComponent : GameSceneComponent
    {
        public TGameScene Scene { get; set; }
        public TGameSceneComponent SceneComponent { get; protected set; }

        public override Task<GameObject> LoadAsset()
        {
            return LoadScene();
        }

        public override Task PreInitialize()
        {
            SceneComponent = GetSceneComponent();
            return base.PreInitialize();
        }

        public override Task Startup()
        {
            return base.Startup();
        }

        public override async Task Terminate()
        {
            await UnloadScene();
        }

        protected virtual Task<GameObject> LoadScene()
        {
            return Task.FromResult<GameObject>(null);
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

        protected override async Task<GameObject> LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = GameObject.Instantiate(_asset);
            GameSceneHelper.MoveToGameRootScene(_instance);
            return _instance;
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

        protected override async Task<GameObject> LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
            return null;
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

        public override async Task<GameObject> LoadAsset()
        {
            await LoadScene();
            return null;
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

    // 主にダイアログ用(オーバーレイ表示想定)
    // Memo: MonoBehaviourを使う以上、C#では多重継承できないので、個別作成
    public abstract class GameDialogScene<TScene, TSceneComponent, TResult> : GameScene<TScene, TSceneComponent>, IGameSceneStartupFilter<TScene, TSceneComponent>, IGameSceneResult<TResult>
        where TScene : IGameScene
        where TSceneComponent : GameSceneComponent
    {
        public Func<TScene, TSceneComponent, Task> StartupFilter { get; set; }

        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        private GameObject _asset;
        private GameObject _instance;

        public override Task<GameObject> LoadAsset()
        {
            return LoadScene();
        }

        public override Task PreInitialize()
        {
            SceneComponent = GetSceneComponent();
            return Task.CompletedTask;
        }

        public override Task Startup()
        {
            StartupFilter?.Invoke(Scene, SceneComponent);
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

        protected override async Task<GameObject> LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = GameObject.Instantiate(_asset);
            return _instance;
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

        protected override TSceneComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TSceneComponent>(_instance);
        }
    }
}