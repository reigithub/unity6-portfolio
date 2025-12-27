using System.Threading.Tasks;
using Game.Core.Scenes;

namespace Sample
{
    public class GameTitleScene : GamePrefabScene<GameTitleScene, GameTitleSceneComponent>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/GameTitleScene.prefab";

        protected internal override Task Initialize()
        {
            SceneComponent.Initialize();

            return base.Initialize();
        }
    }
}