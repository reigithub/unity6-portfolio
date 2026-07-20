using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Shared.Extensions;
using Game.Shared.Scenes;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.MVC.Core.Scenes
{
    public interface IGameScene : IGameSceneState, IGameSceneArgHandler, ICompositeDisposable
    {
        // 事前初期化処理
        // サーバー通信, モデルクラスの初期化など
        UniTask PreInitialize() => UniTask.CompletedTask;

        // アセット(主にこのシーン)をロード
        UniTask LoadAsset() => UniTask.CompletedTask;

        // シーンビュー初期化～起動処理
        UniTask Startup() => UniTask.CompletedTask;

        // 起動後の処理
        // シーン起動後に演出など
        UniTask Ready() => UniTask.CompletedTask;

        UniTask Sleep(bool visible) => UniTask.CompletedTask;

        UniTask Restart() => UniTask.CompletedTask;

        // シーンを終了させて破棄する
        UniTask Terminate() => UniTask.CompletedTask;
    }

    public abstract class GameScene : IGameScene
    {
        protected abstract string AssetPathOrAddress { get; }

        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }

        public CompositeDisposable Disposables { get; protected set; } = new();

        public virtual UniTask PreInitialize() => UniTask.CompletedTask;

        public virtual UniTask LoadAsset() => UniTask.CompletedTask;

        public virtual UniTask Startup() => UniTask.CompletedTask;

        public virtual UniTask Sleep(bool visible) => UniTask.CompletedTask;

        public virtual UniTask Restart() => UniTask.CompletedTask;

        public virtual UniTask Ready() => UniTask.CompletedTask;

        public virtual UniTask Terminate() => UniTask.CompletedTask;
    }

    public interface IGameSceneState
    {
        GameSceneState State { get; set; }
    }

    public interface IGameSceneArg<in TArg>
    {
        UniTask SetArg(TArg arg);
    }

    public interface IGameSceneArgHandler
    {
        Func<IGameScene, UniTask> ArgHandler { get; set; }
    }

    public interface IGameSceneResult
    {
        bool TrySetCanceled();
        bool TrySetException(Exception e);
    }

    public interface IGameSceneResult<TResult> : IGameSceneResult
    {
        TResult Result { get; set; }

        UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        bool TrySetResult(TResult result);
    }

    public interface ICompositeDisposable
    {
        CompositeDisposable Disposables { get; }
    }

    /// <summary>
    /// 遷移演出
    /// GameSceneServiceがTransitionCoreで自動的に呼び出す
    /// </summary>
    public interface IGameSceneFader
    {
        UniTask FadeInAsync(float duration = 0.3f);

        UniTask FadeOutAsync(float duration = 0.3f);
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
            SceneComponent = default;
            SceneComponent = GetSceneComponent();
            if (Disposables.IsDisposed)
                Disposables = new CompositeDisposable();
            await SceneComponent.PreInitialize();
        }

        public override async UniTask Startup()
        {
            await SceneComponent.Startup();
            await base.Startup();
        }

        public override async UniTask Ready()
        {
            await SceneComponent.Ready();
            await base.Ready();
        }

        public override async UniTask Sleep(bool visible)
        {
            await SceneComponent.Sleep(visible);
            await base.Sleep(visible);
        }

        public override async UniTask Restart()
        {
            await SceneComponent.Restart();
            await base.Restart();
        }

        public override async UniTask Terminate()
        {
            await SceneComponent.Terminate();
            await UnloadScene();
            await base.Terminate();
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
        , IGameSceneFader
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        private readonly IAddressableAssetService _assetService = GameServiceManager.Resolve<IAddressableAssetService>();
        private readonly IMessagePipeService _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

        private GameObject _asset;
        private GameObject _instance;

        protected override async UniTask LoadScene()
        {
            _asset = await _assetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            Scene scene = GameSceneHelper.GetGameRootScene();
            _instance = scene.IsValid()
                ? UnityEngine.Object.Instantiate(_asset, new InstantiateParameters { scene = scene })
                : UnityEngine.Object.Instantiate(_asset);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _assetService.Release(_asset);
                _asset = null;
            }

            return UniTask.CompletedTask;
        }

        protected override TGameSceneComponent GetSceneComponent()
            => GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance);

        public async UniTask FadeInAsync(float duration = 0.3f)
            => await _messagePipeService.PublishAsync(new MessageSignals.GameScene.FadeIn());

        public async UniTask FadeOutAsync(float duration = 0.3f)
            => await _messagePipeService.PublishAsync(new MessageSignals.GameScene.FadeOut());
    }

    // コンポーネント付きのUnityScene
    public abstract class GameUnityScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        private readonly IAddressableAssetService _assetService = GameServiceManager.Resolve<IAddressableAssetService>();

        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Additive;

        private SceneInstance _instance;

        protected override async UniTask LoadScene()
        {
            _instance = await _assetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            if (LoadSceneMode is LoadSceneMode.Additive) SceneManager.SetActiveScene(_instance.Scene);
        }

        protected override async UniTask UnloadScene()
        {
            await _assetService.UnloadSceneAsync(_instance);
        }

        protected override TGameSceneComponent GetSceneComponent()
            => GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance.Scene);
    }

    // 主にダイアログ用(オーバーレイ表示想定)
    public abstract class GameDialogScene<TScene, TComponent, TResult> : GameScene<TScene, TComponent>, IGameSceneResult<TResult>
        where TScene : IGameScene
        where TComponent : IGameSceneComponent
    {
        private readonly IAddressableAssetService _assetService = GameServiceManager.Resolve<IAddressableAssetService>();

        public TResult Result { get; set; }
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        private GameObject _asset;
        private GameObject _instance;

        protected override async UniTask LoadScene()
        {
            _asset = await _assetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            Scene scene = GameSceneHelper.GetGameRootScene();
            _instance = scene.IsValid()
                ? UnityEngine.Object.Instantiate(_asset, new InstantiateParameters { scene = scene })
                : UnityEngine.Object.Instantiate(_asset);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _assetService.Release(_asset);
                _asset = null;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// リザルトをセットしてダイアログを閉じる
        /// </summary>
        public bool TrySetResult(TResult result)
        {
            Result = result;
            return ResultTcs?.TrySetResult(result) ?? false;
        }

        /// <summary>
        /// ダイアログをキャンセルして閉じる
        /// </summary>
        public bool TrySetCanceled()
            => ResultTcs?.TrySetCanceled() ?? false;

        public bool TrySetException(Exception e)
            => ResultTcs?.TrySetException(e) ?? false;

        protected override TComponent GetSceneComponent()
            => GameSceneHelper.GetSceneComponent<TComponent>(_instance);
    }
}
