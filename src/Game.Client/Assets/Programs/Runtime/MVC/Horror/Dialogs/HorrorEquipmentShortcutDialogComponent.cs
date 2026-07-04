using Game.Core.Services;
using Game.Horror.Equipment;
using Game.Horror.Inventory;
using Game.Horror.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Interfaces;
using Game.Shared.Services;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// ショートカット登録ダイアログのビュー。D-Pad 4スロットへの登録/解除と現在スロットの追跡を担う。
    /// スロット並びは 1=左 / 2=上 / 3=右 / 4=下（index 0-3）。
    /// </summary>
    public class HorrorEquipmentShortcutDialogComponent : GameSceneComponent
    {
        [SerializeField] private HorrorEquipmentShortcutSlotView[] _slots;

        private InputSystemService _inputService;
        private IScriptableDatabaseService _databaseService;
        private HorrorEquipmentShortcutSaveService _saveService;
        private IHorrorInventorySlotInfo _target;
        private int _currentIndex;

        public void Initialize(IHorrorInventorySlotInfo target)
        {
            _target = target;
            _inputService = GameServiceManager.Get<InputSystemService>();
            _databaseService = GameServiceManager.Get<ScriptableDatabaseService>();
            _saveService = GameServiceManager.Resolve<HorrorEquipmentShortcutSaveService>();

            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slots[i].Initialize();
                RefreshSlot(index);

                _slots[i].OnSelected
                    .Subscribe(_ => _currentIndex = index)
                    .AddTo(Disposables);

                _slots[i].OnSubmit
                    .Subscribe(_ => Register(index))
                    .AddTo(Disposables);
            }

            // 初期フォーカスを先頭スロットへ
            if (_slots.Length > 0)
                _inputService.SetSelectedGameObject(_slots[0].gameObject);
        }

        /// <summary>現在選択中スロットの登録を外す（Dialog の Remove 入力から呼ぶ）。</summary>
        public void RemoveCurrent()
        {
            if (_saveService.Clear(_currentIndex))
                _slots[_currentIndex].SetEmpty();
        }

        // 対象アイテムを指定スロットへ登録し、表示を更新する。
        private void Register(int index)
        {
            if (_target == null) return;
            if (_saveService.Set(index, _target.SlotType, _target.Id))
                _slots[index].SetItem(_target);
        }

        // 保存済み binding を master 解決してスロット表示を更新する（空なら空表示）。
        private void RefreshSlot(int index)
        {
            if (_saveService.TryGet(index, out var slot) && HorrorInventoryHelper.TryGetSlotInfo(_databaseService.Database, slot.SlotType, slot.Id, out var info))
                _slots[index].SetItem(info);
            else
                _slots[index].SetEmpty();
        }
    }
}
