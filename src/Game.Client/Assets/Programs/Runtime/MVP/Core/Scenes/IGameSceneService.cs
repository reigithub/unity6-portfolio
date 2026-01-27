using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Enums;

namespace Game.MVP.Core.Scenes
{
    /// <summary>
    /// シーン遷移管理サービスのインターフェース
    /// MVCのIGameSceneServiceと同等の設計で、VContainerによるDI制約のみが異なる
    /// シーンはnew()制約によりLifetimeScopeへの登録なしでインスタンス化される
    /// </summary>
    public interface IGameSceneService
    {
        /// <summary>
        /// 指定したシーンへ遷移（引数なし、戻り値なし）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <param name="operations">遷移オプション（現在シーンの終了方法、履歴操作）</param>
        UniTask TransitionAsync<TScene>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        /// <summary>
        /// 指定したシーンへ遷移（引数あり、戻り値なし）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <typeparam name="TArg">引数の型</typeparam>
        /// <param name="arg">シーンに渡す引数</param>
        /// <param name="operations">遷移オプション</param>
        UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        /// <summary>
        /// 指定したシーンへ遷移（引数なし、戻り値あり）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <param name="operations">遷移オプション</param>
        /// <returns>シーンからの戻り値</returns>
        UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        /// <summary>
        /// 指定したシーンへ遷移（引数あり、戻り値あり）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <typeparam name="TArg">引数の型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <param name="arg">シーンに渡す引数</param>
        /// <param name="operations">遷移オプション</param>
        /// <returns>シーンからの戻り値</returns>
        UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        /// <summary>
        /// 履歴から前のシーンへ戻る
        /// </summary>
        UniTask TransitionPrevAsync();

        /// <summary>
        /// ダイアログをオーバーレイ表示（引数なし）
        /// </summary>
        /// <typeparam name="TScene">ダイアログシーン型</typeparam>
        /// <typeparam name="TComponent">UIコンポーネント型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <returns>ダイアログの結果</returns>
        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>()
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : IGameSceneComponent;

        /// <summary>
        /// ダイアログをオーバーレイ表示（引数あり）
        /// </summary>
        /// <typeparam name="TScene">ダイアログシーン型</typeparam>
        /// <typeparam name="TComponent">UIコンポーネント型</typeparam>
        /// <typeparam name="TArg">引数の型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <param name="arg">ダイアログに渡す引数</param>
        /// <returns>ダイアログの結果</returns>
        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TArg, TResult>(TArg arg)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, IGameSceneArg<TArg>, new()
            where TComponent : IGameSceneComponent;

        /// <summary>
        /// 指定した型のシーンが処理中かどうかを判定
        /// </summary>
        /// <param name="type">シーン型</param>
        /// <returns>処理中の場合true</returns>
        bool IsProcessing(Type type);

        /// <summary>
        /// 指定した型のシーンを終了
        /// </summary>
        /// <param name="type">終了するシーン型</param>
        /// <param name="clearHistory">履歴もクリアするか</param>
        UniTask TerminateAsync(Type type, bool clearHistory = false);

        /// <summary>
        /// 最後に追加されたシーン（ダイアログ等）を終了
        /// </summary>
        /// <param name="clearHistory">履歴もクリアするか</param>
        UniTask TerminateLastAsync(bool clearHistory = false);

        /// <summary>
        /// すべてのシーンを終了（シャットダウン時に使用）
        /// </summary>
        UniTask TerminateAllAsync();
    }
}