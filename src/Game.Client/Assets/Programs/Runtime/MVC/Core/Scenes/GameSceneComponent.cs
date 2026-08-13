using Cysharp.Threading.Tasks;
using Game.Core.Services;
using R3;
using UnityEngine;

namespace Game.MVC.Core.Scenes
{
    public interface IGameSceneComponent : ICompositeDisposable
    {
        UniTask PreInitialize() => UniTask.CompletedTask;

        UniTask Startup() => UniTask.CompletedTask;

        UniTask Ready() => UniTask.CompletedTask;

        UniTask Sleep(bool visible) => UniTask.CompletedTask;

        UniTask Restart() => UniTask.CompletedTask;

        UniTask Terminate() => UniTask.CompletedTask;
    }

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IInputSystemService _inputService;
        private GameObject _selectedGameObject;

        public CompositeDisposable Disposables { get; } = new();

        public virtual UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            SetInteractable(false, visible: true);
            return UniTask.CompletedTask;
        }

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep(bool visible)
        {
            return Unfocus(visible);
        }

        public virtual UniTask Restart()
        {
            return Focus();
        }

        public virtual UniTask Ready()
        {
            return Focus();
        }

        public virtual UniTask Terminate()
        {
            Disposables?.Dispose();
            SetInteractable(false);
            return UniTask.CompletedTask;
        }

        private async UniTask Focus()
        {
            SetInteractable(true);
            await UniTask.Yield();
            ResolveSelectable();
        }

        private async UniTask Unfocus(bool visible)
        {
            _selectedGameObject = _inputService.GetSelectedGameObject();
            await UniTask.Yield();
            SetInteractable(false, visible);
        }

        protected void ResolveSelectable()
            => _inputService.ResolveControlScheme(_selectedGameObject);

        public virtual void SetInteractable(bool interactable, bool visible = false)
        {
            if (TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                if (interactable)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
                else
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        // public IDisposable BlockInteractable(bool visible = false)
        // {
        //     SetInteractable(false, visible);
        //     return Disposable.Create(() => SetInteractable(true, visible));
        // }
    }
}
