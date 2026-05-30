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

        UniTask Focus() => UniTask.CompletedTask;

        UniTask Unfocus() => UniTask.CompletedTask;
    }

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IAudioService _audioService;
        protected IAudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private Selectable[] _selectables;
        private Selectable[] Selectables => _selectables ??= GetComponentsInChildren<Selectable>();

        public CompositeDisposable Disposables { get; } = new();

        private GameObject _selectedGameObject;

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);

            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            return UniTask.CompletedTask;
        }

        public virtual async UniTask Ready()
        {
            await Focus();
        }

        public virtual UniTask Terminate()
        {
            Disposables?.Dispose();
            SetInteractable(false);
            return UniTask.CompletedTask;
        }

        public virtual async UniTask Focus()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetInteractable(true);
            await UniTask.Yield();
            InputService.ResolveControlScheme(_selectedGameObject);
        }

        public virtual async UniTask Unfocus()
        {
            _selectedGameObject = InputService.GetSelectedGameObject();
            await UniTask.Yield();
            SetInteractable(false);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public virtual void SetInteractable(bool interactive)
        {
            if (Selectables.Length > 0)
            {
                foreach (var selectable in Selectables)
                {
                    selectable.interactable = interactive;
                }
            }
        }

        // public IDisposable BlockInteractable()
        // {
        //     SetInteractable(false);
        //     return Disposable.Create(() => SetInteractable(true));
        // }
    }

    public abstract class UnitySceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private Selectable[] _selectables;
        private Selectable[] Selectables => _selectables ??= GetComponentsInChildren<Selectable>();

        public CompositeDisposable Disposables { get; } = new();

        private GameObject _selectedGameObject;

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        public virtual async UniTask Ready()
        {
            await Focus();
        }

        public virtual UniTask Terminate()
        {
            Disposables?.Dispose();
            SetInteractable(false);
            return UniTask.CompletedTask;
        }

        public virtual async UniTask Focus()
        {
            SetInteractable(true);
            await UniTask.Yield();
            InputService.ResolveControlScheme(_selectedGameObject);
        }

        public virtual async UniTask Unfocus()
        {
            _selectedGameObject = InputService.GetSelectedGameObject();
            await UniTask.Yield();
            SetInteractable(false);
        }

        public virtual void SetInteractable(bool interactive)
        {
            if (Selectables.Length > 0)
            {
                foreach (var selectable in Selectables)
                {
                    selectable.interactable = interactive;
                }
            }
        }
    }
}
