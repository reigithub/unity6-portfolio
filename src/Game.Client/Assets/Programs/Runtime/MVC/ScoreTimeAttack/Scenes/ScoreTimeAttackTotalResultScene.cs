using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.MVC.Core.Scenes;
using Game.ScoreTimeAttack.Services;
using Game.Shared.Bootstrap;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackTotalResultScene : GamePrefabScene<ScoreTimeAttackTotalResultScene, ScoreTimeAttackTotalResultSceneComponent>
    {
        protected override string AssetPathOrAddress => "ScoreTimeAttackTotalResultScene";

        public override UniTask Startup()
        {
            ApplicationEvents.ShowCursor();

            var gameStageService = GameServiceManager.Resolve<IScoreTimeAttackStageService>();
            var totalResult = gameStageService.CreateTotalResult();
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
            GameServiceManager.Unregister<IScoreTimeAttackStageService>();
            return base.Terminate();
        }
    }
}
