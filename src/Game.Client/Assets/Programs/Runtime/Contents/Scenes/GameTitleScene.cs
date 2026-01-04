using System.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;

namespace Game.Contents.Scenes
{
    public class GameTitleScene : GamePrefabScene<GameTitleScene, GameTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "GameTitleScene2";

        public override Task Startup()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, true);
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.Cursor, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, false);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.Escape, false);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.ScrollWheel, false);

            SceneComponent.Initialize();

            return base.Startup();
        }

        public override Task Ready()
        {
            SceneComponent.OnReady();
            return base.Ready();
        }

        public override Task Terminate()
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.Escape, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.ScrollWheel, true);

            return base.Terminate();
        }
    }
}