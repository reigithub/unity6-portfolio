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
        UniTask TransitionAsync<TScene>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : class, IGameScene, new();

        UniTask TransitionPrevAsync();

        /// <summary>
        /// ダイアログを表示（引数なし）
        /// </summary>
        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>()
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : IGameSceneComponent;

        /// <summary>
        /// ダイアログを表示（引数あり）
        /// </summary>
        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TArg, TResult>(TArg arg)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, IGameSceneArg<TArg>, new()
            where TComponent : IGameSceneComponent;

        bool IsProcessing(Type type);

        UniTask TerminateAsync(Type type, bool clearHistory = false);

        UniTask TerminateLastAsync(bool clearHistory = false);

        UniTask TerminateAllAsync();
    }
}