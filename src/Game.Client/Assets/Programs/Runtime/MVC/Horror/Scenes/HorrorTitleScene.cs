using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;

namespace Game.Horror.Scenes
{
    public class HorrorTitleScene : GamePrefabScene<HorrorTitleScene, HorrorTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorTitleScene";

        private IGameSceneService _sceneService;

        public override UniTask PreInitialize()
        {
            _sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            SceneComponent.OnStart
                .SubscribeAwait(async (_, _) =>
                {
                    SceneComponent.SetInteractable(false);
                    await _sceneService.TransitionAsync<HorrorStageScene>();
                })
                .AddTo(Disposables);

            SceneComponent.OnReturn
                .SubscribeAwait(async (_, _) =>
                {
                    SceneComponent.SetInteractable(false);
                    await _sceneService.TerminateLastAsync();
                    await ApplicationEvents.RequestReturnToTitleAsync();
                })
                .AddTo(Disposables);

            SceneComponent.OnQuit
                .Subscribe(_ =>
                {
                    SceneComponent.SetInteractable(false);
                    ApplicationEvents.RequestShutdown();
                })
                .AddTo(Disposables);

            return base.Startup();
        }
    }
}
