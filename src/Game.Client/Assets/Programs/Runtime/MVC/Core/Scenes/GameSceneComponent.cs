using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVC.Core.Scenes
{
    public interface IGameSceneComponent : ICompositeDisposable
    {
        UniTask Startup() => UniTask.CompletedTask;

        UniTask Ready() => UniTask.CompletedTask;

        UniTask Sleep() => UniTask.CompletedTask;

        UniTask Restart() => UniTask.CompletedTask;

        UniTask Terminate() => UniTask.CompletedTask;
    }

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IInputSystemService _inputService;
        private IInputSystemService InputService => _inputService ??= GameServiceManager.Resolve<IInputSystemService>();

        private GameObject _selectedGameObject;

        public CompositeDisposable Disposables { get; } = new();

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            return Unfocus();
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
            InputService.ResolveControlScheme(_selectedGameObject);
        }

        private async UniTask Unfocus()
        {
            _selectedGameObject = InputService.GetSelectedGameObject();
            await UniTask.Yield();
            SetInteractable(false);
        }

        public virtual void SetInteractable(bool interactable)
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
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        // public IDisposable BlockInteractable()
        // {
        //     SetInteractable(false);
        //     return Disposable.Create(() => SetInteractable(true));
        // }
    }
}
