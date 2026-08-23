using System.Linq;
using Game.Core.Services;
using Game.Horror.Equipment;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// ショートカット登録ダイアログのビュー
    /// </summary>
    public class HorrorEquipmentShortcutDialogComponent : GameSceneComponent
    {
        [SerializeField] private HorrorEquipmentSlotView[] _slots;

        private IInputSystemService _inputService;
        private IHorrorEquipmentService _equipmentService;

        /// <summary>スロット決定（クリック）。値は決定されたスロット index。</summary>
        public Observable<int> OnSlotClicked
            => _slots.Select((slot, index) => slot.OnClick.Select(_ => index)).Merge();

        /// <summary>スロット選択（フォーカス移動）。値は選択されたスロット index。</summary>
        public Observable<int> OnSlotSelected
            => _slots.Select((slot, index) => slot.OnSelect.Select(_ => index)).Merge();

        public void Initialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _equipmentService = GameServiceManager.Resolve<IHorrorEquipmentService>();

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Initialize();
                RefreshSlot(i);
            }

            // 初期フォーカスを先頭スロットへ
            if (_slots.Length > 0)
                _inputService.SetSelectedGameObject(_slots[0].gameObject);

            _equipmentService.EquipmentChanged
                .Subscribe(_ => RefreshAllSlots())
                .AddTo(Disposables);
        }

        /// <summary>全スロットの表示を現在の装備状態から再構築する（Dialog の EquipmentChanged 購読から呼ぶ）。</summary>
        private void RefreshAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                RefreshSlot(i);
        }

        // 保存済み binding を master 解決してスロット表示を更新する（空なら空表示）。
        private void RefreshSlot(int index)
        {
            if (_equipmentService.TryGetSlot(index, out var slot))
                _slots[index].SetSlot(slot.ObjectCategory, slot.Id);
            else
                _slots[index].SetEmpty();
        }
    }
}
