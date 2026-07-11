using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackTitleScene : GamePrefabScene<ScoreTimeAttackTitleScene, ScoreTimeAttackTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "ScoreTimeAttackTitleScene";

        private IInputSystemService _inputService;
        private IInputSystemService InputService => _inputService ??= GameServiceManager.Resolve<IInputSystemService>();

        public override UniTask Startup()
        {
            OnEnable();
            SceneComponent.Initialize();

            return base.Startup();
        }

        public override UniTask Sleep()
        {
            OnDisable();
            return base.Sleep();
        }

        public override async UniTask Ready()
        {
            OnEnable();
            await base.Ready();
            await SceneComponent.ReadyAsync();
        }

        public override UniTask Terminate()
        {
            OnDisable();
            return base.Terminate();
        }

        private void OnEnable()
        {
            ApplicationEvents.ResumeTime();
            ApplicationEvents.ShowCursor();
            InputService.UI.Menu.Disable();
            InputService.UI.ScrollWheel.Disable();
        }

        private void OnDisable()
        {
            InputService.UI.Menu.Enable();
            InputService.UI.ScrollWheel.Enable();
        }
    }
}
