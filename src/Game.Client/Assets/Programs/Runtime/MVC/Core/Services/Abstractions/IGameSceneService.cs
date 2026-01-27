using System;
using Cysharp.Threading.Tasks;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    /// <summary>
    /// シーン遷移管理サービスのインターフェース
    /// Prefabシーンの遷移、履歴管理、ダイアログ表示を提供
    /// </summary>
    public interface IGameSceneService : IGameService
    {
        /// <summary>
        /// 指定したシーンへ遷移（引数なし、戻り値なし）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <param name="operations">遷移オプション（現在シーンの終了方法、履歴操作）</param>
        UniTask TransitionAsync<TScene>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        /// <summary>
        /// 指定したシーンへ遷移（引数あり、戻り値なし）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <typeparam name="TArg">引数の型</typeparam>
        /// <param name="arg">シーンに渡す引数</param>
        /// <param name="operations">遷移オプション</param>
        UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        /// <summary>
        /// 指定したシーンへ遷移（引数なし、戻り値あり）
        /// </summary>
        /// <typeparam name="TScene">遷移先シーン型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <param name="operations">遷移オプション</param>
        /// <returns>シーンからの戻り値</returns>
        UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

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
            where TScene : IGameScene, new();

        /// <summary>
        /// 履歴から前のシーンへ戻る
        /// </summary>
        UniTask TransitionPrevAsync();

        /// <summary>
        /// ダイアログをオーバーレイ表示
        /// </summary>
        /// <typeparam name="TScene">ダイアログシーン型</typeparam>
        /// <typeparam name="TComponent">UIコンポーネント型</typeparam>
        /// <typeparam name="TResult">戻り値の型</typeparam>
        /// <param name="initializer">初期化コールバック（オプション）</param>
        /// <returns>ダイアログの結果</returns>
        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>(Func<TComponent, IGameSceneResult<TResult>, UniTask> initializer = null)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
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
        /// Unityシーンを追加読み込み（ステージ用）
        /// </summary>
        /// <param name="sceneName">シーン名またはAddressablesアドレス</param>
        /// <param name="loadSceneMode">読み込みモード</param>
        /// <param name="activateOnLoad">読み込み完了時に自動アクティブ化するか</param>
        /// <returns>読み込まれたシーンインスタンス</returns>
        UniTask<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true);

        /// <summary>
        /// 読み込んだUnityシーンをアンロード
        /// </summary>
        /// <param name="sceneInstance">アンロードするシーンインスタンス</param>
        UniTask UnloadUnitySceneAsync(SceneInstance sceneInstance);

        /// <summary>
        /// 読み込んだすべてのUnityシーンをアンロード
        /// </summary>
        UniTask UnloadUnitySceneAllAsync();
    }
}