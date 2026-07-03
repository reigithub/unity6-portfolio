using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Inventory;
using Game.Horror.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Enums;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorInventoryDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;
        [SerializeField] private CanvasGroup _slotsCanvasGroup;
        [SerializeField] private HorrorInventorySlotView[] _slots;
        [SerializeField] private HorrorInventorySlotDetailView _slotDetailView;
        [SerializeField] private HorrorInventoryContextMenu _contextMenu;

        #endregion

        private InputSystemService _inputService;
        private HorrorInventorySlotView _submittedSlot;

        public Observable<HorrorInventoryContextActionInfo> OnContextActionClicked
            => _contextMenu.OnClicked.Select(x => new HorrorInventoryContextActionInfo
            {
                ContextActionType = x,
                SlotInfo = _submittedSlot.SlotInfo
            });

        public void Initialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();

            _tabGroup.Initialize();
            BindSlots();
            _tabGroup.ChangeTab(0);

            if (_contextMenu != null)
            {
                _contextMenu.OnClosed
                    .Subscribe(_ => OnSubmenuClosed())
                    .AddTo(Disposables);
            }
        }

        public void NextTab() => _tabGroup.NextTab();
        public void PreviousTab() => _tabGroup.PreviousTab();

        private void BindSlots()
        {
            var inventory = GameServiceManager.Resolve<HorrorInventorySaveService>();
            var slots = inventory.Data.Slots;
            var database = GameServiceManager.Get<ScriptableDatabaseService>().Database;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Initialize();

                bool empty = true;
                if (i < slots.Count)
                {
                    var slot = slots[i];
                    switch (slot.SlotType)
                    {
                        case InventorySlotType.Item:
                        {
                            if (database.HorrorItemMasterTable.TryFindById(slot.Id, out var master))
                            {
                                _slots[i].SetSlot(master, slot.Count);
                                empty = false;
                            }
                            break;
                        }
                        case InventorySlotType.Weapon:
                        {
                            if (database.HorrorWeaponMasterTable.TryFindById(slot.Id, out var master))
                            {
                                _slots[i].SetSlot(master, slot.Count);
                                empty = false;
                            }
                            break;
                        }
                    }
                }

                if (empty) _slots[i].SetEmpty();

                _slots[i].OnSelected
                    .Subscribe(UpdateDetail)
                    .AddTo(Disposables);

                _slots[i].OnSubmit
                    .Subscribe(OpenSubmenu)
                    .AddTo(Disposables);
            }

            UpdateDetail(_slots[0]);
        }

        private void UpdateDetail(HorrorInventorySlotView slot)
            => _slotDetailView.SetSlotDetail(slot.SlotInfo);

        public bool IsSubmenuOpen() => _contextMenu != null && _contextMenu.IsOpen;

        // サブメニュー展開：非空スロットの決定で種別に応じたエントリを開く。空スロットは無視。
        private void OpenSubmenu(HorrorInventorySlotView slot)
        {
            if (IsSubmenuOpen()) return;
            if (slot == null || slot.SlotInfo == null) return;

            var entries = slot.SlotInfo.SlotType.ToContextActions();
            if (entries.Length == 0) return;

            _submittedSlot = slot;
            SetSlotsInteractable(false);
            _contextMenu.Open(slot.RectTransform, entries);
        }

        /// <summary>サブメニューを閉じる（キャンセル・アクション確定時に Dialog から呼ぶ）。</summary>
        public void CloseSubmenu()
        {
            if (_contextMenu != null) _contextMenu.Close();
        }

        // 閉じられたらグリッド操作を戻し、フォーカスを起点スロットへ復帰させる。
        private void OnSubmenuClosed()
        {
            SetSlotsInteractable(true);
            if (_submittedSlot != null)
            {
                _inputService.SetSelectedGameObject(_submittedSlot.gameObject);
                _submittedSlot = null;
            }
        }

        private void SetSlotsInteractable(bool value)
        {
            if (_slotsCanvasGroup == null) return;
            _slotsCanvasGroup.interactable = value;
            _slotsCanvasGroup.blocksRaycasts = value;
        }
    }
}
