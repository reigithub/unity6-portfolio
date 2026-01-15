using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorタイトルシーン（Presenter）
    /// MVPパターンでViewとの仲介を行う
    /// </summary>
    public class SurvivorTitleScene : GamePrefabScene<SurvivorTitleScene, SurvivorTitleSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;

        protected override string AssetPathOrAddress => "SurvivorTitleScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            GameRootController.SetDirectionalLightActive(false);

            // Viewのイベントを購読
            SceneComponent.OnStartGameClicked
                .Subscribe(_ => OnStartGame().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReturnClicked
                .Subscribe(_ => OnReturn().Forget())
                .AddTo(Disposables);

            SceneComponent.OnQuitClicked
                .Subscribe(_ => OnQuit())
                .AddTo(Disposables);
        }

        public override UniTask Terminate()
        {
            GameRootController.SetDirectionalLightActive(true);
            return base.Terminate();
        }

        private async UniTaskVoid OnStartGame()
        {
            // SceneComponent.SetInteractables(false);
            // await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
            await UniTask.CompletedTask;
        }

        private async UniTaskVoid OnReturn()
        {
            SceneComponent.SetInteractables(false);
            await ApplicationEvents.RequestReturnToTitleAsync();
        }

        private void OnQuit()
        {
            SceneComponent.SetInteractables(false);
            ApplicationEvents.RequestShutdown();
        }
    }
}