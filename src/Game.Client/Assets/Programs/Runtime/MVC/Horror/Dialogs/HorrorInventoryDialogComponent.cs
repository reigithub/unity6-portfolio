using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Inventory;
using Game.Horror.Item;
using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorInventoryDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;
        [SerializeField] private HorrorItemSlotView[] _slots;
        [SerializeField] private HorrorItemDetailView _detailView;

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
            var entries = GameServiceManager.Get<HorrorInventoryService>().Entries;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < entries.Count)
                    _slots[i].SetItem(entries[i].Master, entries[i].Count);
                else
                    _slots[i].SetEmpty();

                _slots[i].OnSelected
                    .Subscribe(UpdateDetail)
                    .AddTo(Disposables);
            }

            UpdateDetail(_slots[0]);
        }

        private void UpdateDetail(HorrorItemSlotView slot) => _detailView.SetDetail(slot.Item);
    }
}
