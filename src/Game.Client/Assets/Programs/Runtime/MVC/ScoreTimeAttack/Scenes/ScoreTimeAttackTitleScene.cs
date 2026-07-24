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

        public override UniTask Sleep(bool visible)
        {
            OnDisable();
            return base.Sleep(visible);
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
            InputService.Player.Menu.Disable();
            InputService.UI.ScrollWheel.Disable();
        }

        private void OnDisable()
        {
            InputService.Player.Menu.Enable();
            InputService.UI.ScrollWheel.Enable();
        }
    }
}
