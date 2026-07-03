using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Item;
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
        [SerializeField] private HorrorInventorySlotView[] _slots;
        [SerializeField] private HorrorInventorySlotDetailView _detailView;

        #endregion

        public void Initialize()
        {
            _tabGroup.Initialize();
            BindSlots();
            _tabGroup.ChangeTab(0);
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
            }

            UpdateDetail(_slots[0]);
        }

        private void UpdateDetail(HorrorInventorySlotView slot)
            => _detailView.SetDetail(slot.SlotInfo);
    }
}
