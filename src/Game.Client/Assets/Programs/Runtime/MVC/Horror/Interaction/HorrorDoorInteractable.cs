using Cysharp.Threading.Tasks;
using Game.Shared.Extensions;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// 開閉するドア。トグルで開閉状態を切り替える。施錠中（マスターデータの RequiredItemId）は
    /// 必要アイテムを所持していなければ実行不可で、初回実行時に解錠する。
    /// 入力方式はマスターデータ（Toggle 指定）に従い、提示動詞のみ開閉状態で切り替える。
    /// </summary>
    public class HorrorDoorInteractable : InteractableBase
    {
        [SerializeField] private PlayableDirector _openDirector;
        [SerializeField] private PlayableDirector _closeDirector;

        private bool _isOpened;
        private bool _isBlocking;

        protected override void Start()
        {
            base.Start();
        }

        public override bool CanInteract() => HasObject();

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
                await _openDirector.PlayAsync();
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
                await _closeDirector.PlayAsync();
            }
            finally
            {
                _isOpened = false;
                _isBlocking = false;
            }
        }
    }
}
