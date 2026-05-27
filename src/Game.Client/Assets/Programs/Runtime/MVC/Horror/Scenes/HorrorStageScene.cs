using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using R3;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GameUnityScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private IGameSceneService _sceneService;
        private IGameSceneService SceneService => _sceneService ??= GameServiceManager.Get<GameSceneService>();

        public override UniTask Startup()
        {
            SceneComponent.OnReturn
                .SubscribeAwait(async (_, _) => await SceneService.TransitionAsync<HorrorTitleScene>())
                .AddTo(Disposables);

            SceneComponent.OnNext
                .SubscribeAwait(async (_, _) => await SceneService.TransitionAsync<HorrorStageScene>())
                .AddTo(Disposables);

            return UniTask.CompletedTask;
        }
    }
}
