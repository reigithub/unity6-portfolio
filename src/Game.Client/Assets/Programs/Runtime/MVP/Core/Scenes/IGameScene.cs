using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Enums;
using R3;
using VContainer;

namespace Game.MVP.Core.Scenes
{
    public interface IGameScene : IGameSceneState, IGameSceneArgHandler, ICompositeDisposable
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

    public interface IGameSceneResult : IDisposable
    {
    }

    public interface IGameSceneResult<TResult> : IGameSceneResult
    {
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

        public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;

        public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;

        void IDisposable.Dispose()
        {
            TrySetCanceled();
        }
    }

    /// <summary>
    /// シーンが子スコープを持つことを示すインターフェース
    /// モデル等のシーンスコープDI管理を担当
    /// </summary>
    /// <remarks>
    /// ScopedResolverはGameSceneService.TransitionCoreでCompositeDisposableに追加され、
    /// TerminateCoreで自動的にDisposeされるため、実装側での明示的なDisposeは不要
    /// </remarks>
    public interface IGameSceneScope
    {
        /// <summary>
        /// 子スコープのDI設定
        /// </summary>
        void ConfigureScope(IContainerBuilder builder);

        /// <summary>
        /// 子スコープのResolver（GameSceneServiceが注入）
        /// </summary>
        IObjectResolver ScopedResolver { get; set; }
    }

    public interface ICompositeDisposable
    {
        public CompositeDisposable Disposables { get; }
    }

    /// <summary>
    /// フェード対応シーンを示すインターフェース
    /// GameSceneServiceがTransitionCoreで自動的に呼び出す
    /// </summary>
    public interface IGameSceneFader
    {
        UniTask FadeInAsync(float duration = 0.3f);
        UniTask FadeOutAsync(float duration = 0.3f);
    }
}