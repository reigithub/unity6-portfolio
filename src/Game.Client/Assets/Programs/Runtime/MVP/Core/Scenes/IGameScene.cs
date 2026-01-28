using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Enums;
using R3;
using VContainer;

namespace Game.MVP.Core.Scenes
{
    /// <summary>
    /// ゲームシーンの基本インターフェース
    /// シーンのライフサイクル（初期化→ロード→起動→準備完了→休止→再起動→終了）を管理
    /// </summary>
    public interface IGameScene : IGameSceneState, IGameSceneArgHandler, ICompositeDisposable
    {
        /// <summary>
        /// 事前初期化処理
        /// サーバー通信、モデルクラスの初期化などを行う
        /// </summary>
        /// <returns>初期化完了を待機するタスク</returns>
        public UniTask PreInitialize()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// アセットをロードする
        /// 主にこのシーンで使用するプレハブやリソースをロード
        /// </summary>
        /// <returns>ロード完了を待機するタスク</returns>
        public UniTask LoadAsset()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンビューの初期化と起動処理
        /// UIの配置やゲームオブジェクトの生成を行う
        /// </summary>
        /// <returns>起動完了を待機するタスク</returns>
        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 起動後の処理
        /// シーン起動後の演出（フェードイン等）やゲーム開始処理を行う
        /// </summary>
        /// <returns>準備完了を待機するタスク</returns>
        public UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンを休止状態にする
        /// 別シーンがオーバーレイ表示される際等に呼び出される
        /// </summary>
        /// <returns>休止完了を待機するタスク</returns>
        public UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンを再起動する
        /// 休止状態から復帰する際に呼び出される
        /// </summary>
        /// <returns>再起動完了を待機するタスク</returns>
        public UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンを終了させて破棄する
        /// リソースの解放やクリーンアップ処理を行う
        /// </summary>
        /// <returns>終了完了を待機するタスク</returns>
        public UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// シーンの状態を保持するインターフェース
    /// </summary>
    public interface IGameSceneState
    {
        /// <summary>
        /// シーンの現在の状態（None/Initializing/Running/Sleeping/Terminating等）
        /// </summary>
        GameSceneState State { get; set; }
    }

    /// <summary>
    /// シーン遷移時に引数を受け取るインターフェース
    /// </summary>
    /// <typeparam name="TArg">引数の型</typeparam>
    public interface IGameSceneArg<in TArg>
    {
        /// <summary>
        /// 遷移時の引数を処理する
        /// </summary>
        /// <param name="arg">遷移元から渡された引数</param>
        /// <returns>引数処理完了を待機するタスク</returns>
        public UniTask ArgHandle(TArg arg);
    }

    /// <summary>
    /// シーン引数ハンドラーを保持するインターフェース
    /// GameSceneServiceがArgHandleを呼び出すために使用
    /// </summary>
    public interface IGameSceneArgHandler
    {
        /// <summary>
        /// 引数ハンドラー関数（GameSceneServiceが設定）
        /// </summary>
        public Func<IGameScene, UniTask> ArgHandler { get; set; }
    }

    /// <summary>
    /// シーン結果を返すインターフェースの基底
    /// ダイアログシーン等で結果を返す際に使用
    /// </summary>
    public interface IGameSceneResult : IDisposable
    {
    }

    /// <summary>
    /// 型付きシーン結果を返すインターフェース
    /// </summary>
    /// <typeparam name="TResult">結果の型</typeparam>
    public interface IGameSceneResult<TResult> : IGameSceneResult
    {
        /// <summary>
        /// 結果を設定するためのCompletionSource
        /// </summary>
        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        /// <summary>
        /// 結果を設定する
        /// </summary>
        /// <param name="result">シーンの結果</param>
        /// <returns>設定に成功した場合true</returns>
        public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

        /// <summary>
        /// キャンセルを設定する
        /// </summary>
        /// <returns>設定に成功した場合true</returns>
        public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;

        /// <summary>
        /// 例外を設定する
        /// </summary>
        /// <param name="e">発生した例外</param>
        /// <returns>設定に成功した場合true</returns>
        public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;

        /// <inheritdoc />
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

    /// <summary>
    /// 複数のDisposableを管理するインターフェース
    /// シーン終了時に一括でDisposeされる
    /// </summary>
    public interface ICompositeDisposable
    {
        /// <summary>
        /// Disposableのコレクション
        /// AddToで登録されたオブジェクトがシーン終了時に破棄される
        /// </summary>
        public CompositeDisposable Disposables { get; }
    }

    /// <summary>
    /// フェード対応シーンを示すインターフェース
    /// GameSceneServiceがTransitionCoreで自動的に呼び出す
    /// </summary>
    public interface IGameSceneFader
    {
        /// <summary>
        /// フェードイン（画面を表示）を実行する
        /// </summary>
        /// <param name="duration">フェード時間（秒）</param>
        /// <returns>フェード完了を待機するタスク</returns>
        UniTask FadeInAsync(float duration = 0.3f);

        /// <summary>
        /// フェードアウト（画面を非表示）を実行する
        /// </summary>
        /// <param name="duration">フェード時間（秒）</param>
        /// <returns>フェード完了を待機するタスク</returns>
        UniTask FadeOutAsync(float duration = 0.3f);
    }
}