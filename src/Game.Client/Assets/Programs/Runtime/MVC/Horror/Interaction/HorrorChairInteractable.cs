using Cysharp.Threading.Tasks;
using Game.Shared.Extensions;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.Horror.Interaction
{
    public class HorrorChairInteractable : InteractableBase
    {
        [SerializeField] private PlayableDirector _pushDirector;
        [SerializeField] private PlayableDirector _pullDirector;

        private bool _isOpened;
        private bool _isBlocking;

        protected override void Start()
        {
            base.Start();
        }

        public override bool CanInteract() => HasItem();

        public override void Interact()
        {
            if (_isBlocking || !CanInteract()) return;

            if (!_isOpened)
                OpenAsync().Forget();
            else
                CloseAsync().Forget();

            base.Interact();
        }

        private async UniTask OpenAsync()
        {
            try
            {
                _isBlocking = true;
                SetInteractionToggle(true);
                await _pushDirector.PlayAsync();
            }
            finally
            {
                _isOpened = true;
                _isBlocking = false;
            }
        }

        private async UniTask CloseAsync()
        {
            try
            {
                _isBlocking = true;
                SetInteractionToggle(false);
                await _pullDirector.PlayAsync();
            }
            finally
            {
                _isOpened = false;
                _isBlocking = false;
            }
        }
    }
}
