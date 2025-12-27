using System.Threading.Tasks;
using Game.Core.Scenes;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Sample
{
    public class GameStageScene : GamePrefabScene<GameStageScene, GameStageSceneComponent>, IGameSceneArgs<string>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/GameStageScene.prefab";

        private string _stageName;
        private SceneInstance _stageSceneInstance;

        public Task PreInitialize(string stageName)
        {
            _stageName = stageName;
            return Task.CompletedTask;
        }

        protected internal override async Task LoadAsset()
        {
            await base.LoadAsset();
            _stageSceneInstance = await AssetService.LoadSceneAsync(_stageName);
        }

        protected internal override async Task Initialize()
        {
            var playerStart = GameSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            await SceneComponent.Initialize(playerStart);
        }

        protected internal override async Task Terminate()
        {
            await base.Terminate();
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
        }
    }
}