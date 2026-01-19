using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVC.Core.Scenes
{
    public interface IGameSceneComponent
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

    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private IAudioService _audioService;
        protected IAudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private Button[] _buttons;

        protected Button[] Buttons => _buttons ??= gameObject.GetComponentsInChildren<Button>();

        private void Start()
        {
            if (Buttons.Length > 0)
            {
                Buttons.Select(x => x.OnClickAsObservable())
                    .Merge()
                    .SubscribeAwait(async (_, token) => { await AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton, token); })
                    .AddTo(this);
            }
        }

        public virtual void SetInteractables(bool interactive)
        {
            foreach (var button in Buttons)
            {
                button.interactable = interactive;
            }
        }

        public virtual UniTask Startup()
        {
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
            return UniTask.CompletedTask;
        }
    }
}