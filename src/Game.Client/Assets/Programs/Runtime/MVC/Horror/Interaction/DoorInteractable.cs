using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// 開閉するドア。トグルで開閉状態を切り替える。施錠中（マスターデータの RequiredItemId）は
    /// 必要アイテムを所持していなければ実行不可で、初回実行時に解錠する。
    /// 入力方式はマスターデータ（Toggle 指定）に従い、提示動詞のみ開閉状態で切り替える。
    /// </summary>
    public class DoorInteractable : InteractableBase
    {
        [Tooltip("開閉アニメーション（任意）。bool パラメータ IsOpen を駆動する")]
        [SerializeField] private Animator _animator;

        private static readonly int IsOpenParam = Animator.StringToHash("IsOpen");

        private bool _isOpen;
        private bool _unlocked;

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

            _isOpen = !_isOpen;

            if (_animator != null)
                _animator.SetBool(IsOpenParam, _isOpen);
        }
    }
}
