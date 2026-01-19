using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackTitleScene : GamePrefabScene<ScoreTimeAttackTitleScene, ScoreTimeAttackTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "ScoreTimeAttackTitleScene";

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

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
            MessagePipeService.Publish(MessageKey.System.DirectionalLight, false);
            MessagePipeService.Publish(MessageKey.InputSystem.Escape, false);
            MessagePipeService.Publish(MessageKey.InputSystem.ScrollWheel, false);
        }

        private void OnDisable()
        {
            MessagePipeService.Publish(MessageKey.System.DirectionalLight, true);
            MessagePipeService.Publish(MessageKey.InputSystem.Escape, true);
            MessagePipeService.Publish(MessageKey.InputSystem.ScrollWheel, true);
        }
    }
}