using System;
using Cysharp.Threading.Tasks;
using R3;

namespace Game.ScoreTimeAttack.Base
{
    /// <summary>
    /// Presenter基底クラス
    /// VContainer経由で依存性を注入し、View-Model間の仲介を行う
    /// </summary>
    public abstract class PresenterBase<TView> : IPresenterLifecycle, IDisposable
        where TView : class, IView
    {
        protected TView View { get; private set; }
        protected CompositeDisposable Disposables { get; } = new();

        protected bool IsInitialized { get; private set; }

        /// <summary>
        /// Viewをバインド（VContainer経由またはファクトリから呼び出し）
        /// </summary>
        public void BindView(TView view)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        public virtual UniTask InitializeAsync()
        {
            if (IsInitialized) return UniTask.CompletedTask;

            IsInitialized = true;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 開始処理（Viewのイベント購読、Modelのバインドなど）
        /// </summary>
        public virtual UniTask StartAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 終了処理
        /// </summary>
        public virtual UniTask DisposeAsync()
        {
            Dispose();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    /// <summary>
    /// Model付きPresenter基底クラス
    /// </summary>
    public abstract class PresenterBase<TView, TModel> : PresenterBase<TView>
        where TView : class, IView
        where TModel : class, IDisposable, new()
    {
        protected TModel Model { get; private set; }

        public override UniTask InitializeAsync()
        {
            Model = new TModel();
            return base.InitializeAsync();
        }

        public override UniTask DisposeAsync()
        {
            Model?.Dispose();
            return base.DisposeAsync();
        }
    }
}