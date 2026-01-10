using System;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Scenes;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    /// <summary>
    /// シーン遷移管理サービスのインターフェース
    /// </summary>
    public interface IGameSceneService : IGameService
    {
        UniTask TransitionAsync<TScene>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        UniTask TransitionAsync<TScene, TArg>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        UniTask<TResult> TransitionAsync<TScene, TResult>(GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        UniTask<TResult> TransitionAsync<TScene, TArg, TResult>(TArg arg, GameSceneOperations operations = GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory)
            where TScene : IGameScene, new();

        UniTask TransitionPrevAsync();

        UniTask<TResult> TransitionDialogAsync<TScene, TComponent, TResult>(Func<TComponent, IGameSceneResult<TResult>, UniTask> initializer = null)
            where TScene : GameDialogScene<TScene, TComponent, TResult>, new()
            where TComponent : IGameSceneComponent;

        bool IsProcessing(Type type);

        UniTask TerminateAsync(Type type, bool clearHistory = false);

        UniTask TerminateLastAsync(bool clearHistory = false);

        UniTask<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true);

        UniTask UnloadUnitySceneAsync(SceneInstance sceneInstance);

        UniTask UnloadUnitySceneAllAsync();
    }
}