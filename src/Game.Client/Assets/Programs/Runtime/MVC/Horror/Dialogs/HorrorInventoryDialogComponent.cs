using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Item;
using Game.Horror.SaveData;
using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorInventoryDialogComponent : GameSceneComponent
    {
        #region SerializeField

        [SerializeField] private TabGroup _tabGroup;
        [SerializeField] private HorrorItemSlotView[] _slots;
        [SerializeField] private HorrorItemDetailView _detailView;
        [SerializeField] private Button _saveButton;

        #endregion

        /// <summary>デバッグ用セーブボタンの押下通知（連打防止付き）。</summary>
        public Observable<Unit> OnSaveRequested => _saveButton.OnClickAsObservable();

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
            var items = GameServiceManager.Resolve<HorrorInventorySaveService>().Data.Items;
            var database = GameServiceManager.Get<ScriptableDatabaseService>().Database;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool empty = true;
                if (i < items.Count)
                {
                    var entry = items[i];
                    if (database.HorrorItemMasterTable.TryFindById(entry.ItemId, out var master))
                    {
                        _slots[i].SetItem(master, entry.Count);
                        empty = false;
                    }
                }

                if (empty) _slots[i].SetEmpty();

                _slots[i].OnSelected
                    .Subscribe(UpdateDetail)
                    .AddTo(Disposables);
            }

            UpdateDetail(_slots[0]);
        }

        private void UpdateDetail(HorrorItemSlotView slot)
            => _detailView.SetDetail(slot.Item);
    }
}
