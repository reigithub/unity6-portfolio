using Cysharp.Threading.Tasks;

namespace Game.MVP.Core.Scenes
{
    public interface IGameSceneComponent : ICompositeDisposable
    {
        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }

        // ボタンなどのインタラクティブUI有効化を切り替える
        public void SetInteractables(bool interactable);
    }
}