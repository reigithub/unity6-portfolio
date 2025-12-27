using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Scenes;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Services
{
    public class GameSceneService<TGameScene> : GameService
        where TGameScene : GameScene
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        protected AddressableAssetService AssetService => _assetService.Reference;

        protected internal override bool AllowResidentOnMemory => true;
    }

    /// <summary>
    /// GameSceneの遷移挙動を制御するサービス
    /// </summary>
    public partial class GameSceneService : GameSceneService<GameScene>
    {
        private readonly List<GameScene> _gameScenes = new();
        private readonly List<SceneInstance> _unityScenes = new();

        public async Task TransitionAsync<TGameScene>()
            where TGameScene : GameScene
        {
            // Memo: いずれ履歴を持って、一つ前のシーンへ戻れるようにする？
            await TerminateAllAsync();

            var gameScene = GameSceneHelper.CreateInstance(typeof(TGameScene));
            if (gameScene is not null)
            {
                await gameScene.LoadAsset();
                await gameScene.PreInitialize();
                await gameScene.Initialize();
                await gameScene.PostInitialize();
                _gameScenes.Add(gameScene);
            }
        }

        public async Task TransitionAsync<TGameScene, TGameSceneArgs>(TGameSceneArgs args)
            where TGameScene : GameScene, IGameSceneArgs<TGameSceneArgs>
        {
            await TerminateAllAsync();

            var gameScene = GameSceneHelper.CreateInstance(typeof(TGameScene));
            if (gameScene is not null)
            {
                await ((TGameScene)gameScene).PreInitialize(args);

                await gameScene.LoadAsset();
                await gameScene.PreInitialize();
                await gameScene.Initialize();
                await gameScene.PostInitialize();
                _gameScenes.Add(gameScene);
            }
        }

        private async Task TerminateAllAsync()
        {
            foreach (var s in _gameScenes)
            {
                await s.Terminate();
            }

            _gameScenes.Clear();
        }

        public async Task<SceneInstance> LoadUnitySceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true)
        {
            var sceneInstance = await AssetService.LoadSceneAsync(sceneName, loadSceneMode, activateOnLoad);
            if (sceneInstance.Scene.IsValid())
            {
                _unityScenes.Add(sceneInstance);
            }

            return sceneInstance;
        }

        public async Task UnloadUnitySceneAsync(SceneInstance sceneInstance)
        {
            await AssetService.UnloadSceneAsync(sceneInstance);

            if (_unityScenes.Contains(sceneInstance))
                _unityScenes.Remove(sceneInstance);
        }

        public async Task UnloadUnitySceneAllAsync()
        {
            foreach (var sceneInstance in _unityScenes)
            {
                await AssetService.UnloadSceneAsync(sceneInstance);
            }

            _unityScenes.Clear();
        }
    }
}