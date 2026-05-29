using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Player;
using Game.MVC.Core.Scenes;
using Game.Shared.Scenes;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GamePrefabScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private IGameSceneService _sceneService;
        private IGameSceneService SceneService => _sceneService ??= GameServiceManager.Get<GameSceneService>();

        private SceneInstance _stageSceneInstance;

        public override async UniTask Startup()
        {
            SceneComponent.OnReturn
                // .SubscribeAwait(async (_, _) => await SceneService.TransitionAsync<HorrorTitleScene>())
                .SubscribeAwait(async (_, _) => await SceneService.TransitionPrevAsync())
                .AddTo(Disposables);

            await LoadUnitySceneAsync();
            await LoadPlayerAsync();
            await base.Startup();
        }

        public override async UniTask Terminate()
        {
            await UnloadUnitySceneAsync();
            await base.Terminate();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            _stageSceneInstance = await AssetService.LoadSceneAsync("Abandoned_Asylum");
            SceneManager.SetActiveScene(_stageSceneInstance.Scene);
        }

        private async UniTask UnloadUnitySceneAsync()
        {
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
        }

        private async UniTask LoadPlayerAsync()
        {
            var playerStart = GameSceneHelper.GetComponentInChildren<HorrorPlayerStart>(_stageSceneInstance.Scene);
            await playerStart.LoadPlayerAsync();
        }
    }
}
