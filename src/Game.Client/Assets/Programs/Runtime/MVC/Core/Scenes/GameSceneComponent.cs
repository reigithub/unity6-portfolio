using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Shared.Input;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
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

        // ボタンなどのインタラクティブUI有効化を切り替える
        void SetInteractable(bool interactable);
    }

    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IAudioService _audioService;
        protected IAudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private Selectable[] _selectables;
        private Selectable[] Selectables => _selectables ??= GetComponentsInChildren<Selectable>();

        private Button[] _buttons;
        private Button[] Buttons => _buttons ??= GetComponentsInChildren<Button>();

        public CompositeDisposable Disposables { get; } = new();

        public virtual UniTask Startup()
        {
            if (Buttons.Length > 0)
            {
                Buttons
                    .Select(x => x.OnClickAsObservable())
                    .Merge()
                    .SubscribeAwait(async (_, token) => { await AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton, token); })
                    .AddTo(Disposables);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            OnFocusExit();
            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                SetInteractable(true);
            }

            OnFocusEnter();
            return UniTask.CompletedTask;
        }

        public virtual UniTask Ready()
        {
            OnFocusEnter();
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate()
        {
            OnFocusExit();
            Disposables?.Dispose();
            return UniTask.CompletedTask;
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

        protected IDisposable BlockInteractables()
        {
            SetInteractable(false);
            return Disposable.Create(() => SetInteractable(true));
        }

        protected IDisposable BlockFocus()
        {
            OnFocusExit();
            return Disposable.Create(() => OnFocusEnter());
        }

        public void OnFocusEnter()
        {
            if (Selectables.Length > 0)
            {
                InputService.SubscribeSelectable();
                InputService.ResolveSelectable(Selectables);

                foreach (var selectable in Selectables)
                {
                    Debug.Log("Selectable: " + selectable.gameObject.name);
                }
            }
        }

        public void OnFocusExit()
        {
            // InputService.SetSelectedGameObject(null);
            InputService.DisposeSelectable();
        }
    }
}
