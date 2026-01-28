using Cysharp.Threading.Tasks;

namespace Game.MVP.Core.Scenes
{
    /// <summary>
    /// シーンコンポーネント（View）の基本インターフェース
    /// UIやビジュアル要素のライフサイクルを管理
    /// </summary>
    public interface IGameSceneComponent : ICompositeDisposable
    {
        /// <summary>
        /// コンポーネントを起動する
        /// UI要素の初期化やイベント登録を行う
        /// </summary>
        /// <returns>起動完了を待機するタスク</returns>
        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 起動後の準備完了処理
        /// アニメーション開始やフォーカス設定等を行う
        /// </summary>
        /// <returns>準備完了を待機するタスク</returns>
        public UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// コンポーネントを休止状態にする
        /// 別画面がオーバーレイ表示される際のUI無効化等を行う
        /// </summary>
        /// <returns>休止完了を待機するタスク</returns>
        public UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// コンポーネントを再起動する
        /// 休止状態からの復帰処理を行う
        /// </summary>
        /// <returns>再起動完了を待機するタスク</returns>
        public UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// コンポーネントを終了して破棄する
        /// イベント解除やリソース解放を行う
        /// </summary>
        /// <returns>終了完了を待機するタスク</returns>
        public UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// ボタンなどのインタラクティブUI要素の有効/無効を切り替える
        /// シーン遷移中やダイアログ表示中に使用
        /// </summary>
        /// <param name="interactable">true: 操作可能, false: 操作不可</param>
        public void SetInteractables(bool interactable);
    }
}