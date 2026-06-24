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
    public class DoorInteractable : InteractableBase
    {
        [SerializeField] private PlayableDirector _openDirector;
        [SerializeField] private PlayableDirector _closeDirector;

        private bool _isOpen;
        private bool _unlocked;
        private bool _isBlocking;

        protected override void Start()
        {
            base.Start();
            _unlocked = Master == null || Master.RequiredItemId == 0;
        }

        public override bool CanInteract() =>
            _unlocked || (Master != null && InventoryHas(Master.RequiredItemId));

        public override void Interact()
        {
            if (!_unlocked)
            {
                if (Master == null || !InventoryHas(Master.RequiredItemId))
                    return;

                _unlocked = true;
            }

            if (_isBlocking) return;

            if (!_isOpen)
                OpenAsync().Forget();
            else
                CloseAsync().Forget();
        }

        private async UniTask OpenAsync()
        {
            try
            {
                _isBlocking = true;
                await _openDirector.PlayAsync();
            }
            finally
            {
                _isOpen = true;
                _isBlocking = false;
            }
        }

        private async UniTask CloseAsync()
        {
            try
            {
                _isBlocking = true;
                await _closeDirector.PlayAsync();
            }
            finally
            {
                _isOpen = false;
                _isBlocking = false;
            }
        }
    }
}
