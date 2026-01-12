using System;
using Cysharp.Threading.Tasks;
using Game.Core.Scenes;
using Game.MVP.Core.Enums;
using R3;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Game.ScoreTimeAttack.Base
{
    /// <summary>
    /// GameSceneと統合したPresenter基底クラス
    /// IGameSceneインターフェースを実装し、GameSceneServiceと連携する
    /// プレハブアセットのロード・破棄は基底クラスで行う
    /// </summary>
    public abstract class GameScenePresenter<TView> : IGameScene, IPresenterLifecycle, IDisposable
        where TView : class, IView
    {
        protected TView View { get; private set; }
        protected CompositeDisposable Disposables { get; } = new();

        protected abstract string AssetPathOrAddress { get; }

        // IGameScene implementation
        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }

        // プレハブアセット管理
        private GameObject _prefabAsset;
        private GameObject _prefabInstance;

        /// <summary>
        /// プレハブインスタンスを取得（派生クラス用）
        /// </summary>
        protected GameObject PrefabInstance => _prefabInstance;

        /// <summary>
        /// Viewをバインド（派生クラスから手動でバインドする場合）
        /// </summary>
        protected void BindView(TView view)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        #region IGameScene Lifecycle

        public virtual UniTask PreInitialize() => UniTask.CompletedTask;

        /// <summary>
        /// プレハブアセットをロードしてViewをバインド
        /// 派生クラスで追加ロードが必要な場合はoverrideしてbase.LoadAsset()を呼び出す
        /// </summary>
        public virtual async UniTask LoadAsset()
        {
            // _prefabAsset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            // _prefabInstance = Object.Instantiate(_prefabAsset);

            // Viewを取得してバインド
            var view = GetViewComponent();
            BindView(view);
        }

        public virtual UniTask Startup() => UniTask.CompletedTask;

        public virtual UniTask Ready() => UniTask.CompletedTask;

        public virtual UniTask Sleep() => UniTask.CompletedTask;

        public virtual UniTask Restart() => UniTask.CompletedTask;

        /// <summary>
        /// プレハブアセットを破棄
        /// 派生クラスで追加破棄が必要な場合はoverrideしてbase.Terminate()を呼び出す
        /// </summary>
        public virtual UniTask Terminate()
        {
            UnloadPrefab();
            Dispose();
            return UniTask.CompletedTask;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// プレハブインスタンスからViewコンポーネントを取得
        /// デフォルトはGetComponentInChildrenで取得
        /// 派生クラスで取得方法を変更したい場合はoverrideする
        /// </summary>
        protected virtual TView GetViewComponent()
        {
            return _prefabInstance.GetComponentInChildren<TView>();
        }

        /// <summary>
        /// プレハブアセットを破棄
        /// </summary>
        protected void UnloadPrefab()
        {
            if (_prefabInstance != null)
            {
                Object.Destroy(_prefabInstance);
                _prefabInstance = null;
                _prefabAsset = null;
            }
        }

        #endregion

        #region IPresenterLifecycle

        public virtual UniTask InitializeAsync() => PreInitialize();

        public virtual UniTask StartAsync() => Ready();

        public virtual UniTask DisposeAsync() => Terminate();

        #endregion

        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    /// <summary>
    /// Model付きGameScenePresenter
    /// </summary>
    public abstract class GameScenePresenter<TView, TModel> : GameScenePresenter<TView>
        where TView : class, IView
        where TModel : class, IDisposable
    {
        protected TModel Model { get; private set; }

        protected void SetModel(TModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public override UniTask Terminate()
        {
            Model?.Dispose();
            return base.Terminate();
        }
    }

    /// <summary>
    /// 結果を返すPresenter用のヘルパー拡張
    /// IGameSceneResult&lt;TResult&gt;を実装するPresenterで使用
    /// </summary>
    public static class GameSceneResultExtensions
    {
        /// <summary>
        /// 結果を設定してダイアログを閉じる
        /// </summary>
        public static bool TrySetResult<TResult>(this IGameSceneResult<TResult> presenter, TResult result)
        {
            return presenter.ResultTcs?.TrySetResult(result) ?? false;
        }

        /// <summary>
        /// キャンセルしてダイアログを閉じる
        /// </summary>
        public static bool TrySetCanceled<TResult>(this IGameSceneResult<TResult> presenter)
        {
            return presenter.ResultTcs?.TrySetCanceled() ?? false;
        }
    }
}