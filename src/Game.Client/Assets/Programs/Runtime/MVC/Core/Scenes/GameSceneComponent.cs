using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Library.Shared.Enums;
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
        void SetInteractables(bool interactable);
    }

    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IAudioService _audioService;
        protected IAudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private Selectable[] _selectables;
        private Selectable[] Selectables => _selectables ??= gameObject.GetComponentsInChildren<Selectable>();

        public CompositeDisposable Disposables { get; } = new();

        public virtual UniTask Startup()
        {
            if (Selectables.Length > 0)
            {
                Selectables
                    .Select(x => x.TryGetComponent(out Button button) ? button : null)
                    .Where(x => x != null)
                    .Select(x => x.OnClickAsObservable())
                    .Merge()
                    .SubscribeAwait(async (_, token) => { await AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton, token); })
                    .AddTo(Disposables);

                EventSystem.current.SetSelectedGameObject(_selectables[0].gameObject);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                SetInteractables(true);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate()
        {
            Disposables?.Dispose();
            return UniTask.CompletedTask;
        }

        public virtual void SetInteractables(bool interactive)
        {
            foreach (var selectable in Selectables)
            {
                selectable.interactable = interactive;
            }
        }
    }
}
