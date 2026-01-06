using System.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;

namespace Game.Contents.Scenes
{
    public class GameTitleScene : GamePrefabScene<GameTitleScene, GameTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "GameTitleScene";

        public override Task Startup()
        {
            OnEnable();
            SceneComponent.Initialize();

            return base.Startup();
        }

        public override Task Sleep()
        {
            OnDisable();
            return base.Sleep();
        }

        public override async Task Ready()
        {
            OnEnable();
            await base.Ready();
            await SceneComponent.ReadyAsync();
        }

        public override Task Terminate()
        {
            OnDisable();
            return base.Terminate();
        }

        private void OnEnable()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, true);
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.Cursor, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, false);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.Escape, false);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.ScrollWheel, false);
        }

        private void OnDisable()
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.Escape, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.ScrollWheel, true);
        }
    }
}