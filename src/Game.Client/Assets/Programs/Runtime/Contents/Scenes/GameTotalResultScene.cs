using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;

namespace Game.Contents.Scenes
{
    public class GameTotalResultScene : GamePrefabScene<GameTotalResultScene, GameTotalResultSceneComponent>
    {
        protected override string AssetPathOrAddress => "GameTotalResultScene";

        public override UniTask Startup()
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, false);

            var totalResult = GlobalMessageBroker.GetRequestHandler<GameStageTotalResultRequest, GameStageTotalResultData>().Invoke(new GameStageTotalResultRequest());
            SceneComponent.Initialize(totalResult);
            return base.Startup();
        }

        public override UniTask Ready()
        {
            SceneComponent.Ready();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DirectionalLight, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.GameStageService.Shutdown, true);
            return base.Terminate();
        }
    }
}