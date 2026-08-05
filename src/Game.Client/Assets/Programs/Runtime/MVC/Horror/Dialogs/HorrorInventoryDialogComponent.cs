using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Inventory;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Scenes;
using Game.Shared.Enums;
using Game.Shared.Extensions;
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

        [SerializeField] private Transform _keyItemContentRoot;
        [SerializeField] private HorrorKeyItemView _keyItemPrefab;

        [SerializeField] private HorrorCraftView _craftView;

        #endregion

        private IInputSystemService _inputService;
        private IHorrorInventoryService _inventoryService;
        private IHorrorKeyItemService _keyItemService;

        private HorrorInventorySlotView _selectedSlot;

        public Observable<HorrorInventoryContextActionInfo> OnContextActionClicked
            => _contextMenu.OnClicked.Select(x => new HorrorInventoryContextActionInfo
            {
                ContextActionType = x,
                SlotView = _selectedSlot
            });

        public void Initialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            _keyItemService = GameServiceManager.Resolve<IHorrorKeyItemService>();

            _tabGroup.Initialize();
            BindSlots();
            _slotDetailView.Initialize();
            UpdateDetail(_slots[0]);
            BindKeyItems();
            BindCraft();
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

        public bool IsProcessing() => IsSubmenuOpen() || IsCrafting();

        #region InventorySlots

        // 初期化と購読は寿命中 1 回のみ。再実行すると Disposables に購読が重複登録されるため、
        // データの再反映は ApplySlots を使うこと。
        private void BindSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SlotIndex = i; // グリッド固定位置。以後不変
                _slots[i].Initialize();
                _slots[i].OnSelected.Subscribe(UpdateDetail).AddTo(Disposables);
                _slots[i].OnSubmit.Subscribe(OpenSubmenu).AddTo(Disposables);
            }

            ApplySlots();
        }

        /// <summary>
        /// インベントリデータをスロット表示へ反映する（再入可能）。
        /// スロット破棄などでデータが変わった後に呼び、グリッドと詳細ペインを最新化する。
        /// </summary>
        public void ApplySlots()
        {
            // 位置（SlotNo）→行の一時テーブルを構築して View と 1:1 で対応させる。範囲外の行は表示しない（正規化後は発生しない）
            var rows = new HorrorInventorySlotData[_slots.Length];
            foreach (var row in _inventoryService.Slots)
            {
                if (row.SlotNo >= 0 && row.SlotNo < rows.Length)
                    rows[row.SlotNo] = row;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (rows[i] != null)
                    _slots[i].SetSlot(rows[i].ObjectCategory, rows[i].Id, rows[i].Count);
                else
                    _slots[i].SetEmpty();
            }

            if (_selectedSlot != null)
                UpdateDetail(_selectedSlot);
        }

        // 入力デバイス変更などによるアイコンの再解決のみ行う（個数テキストは更新しない）。
        // データ変更の反映には ApplySlots を使うこと。
        public void RefreshSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].RefreshSlot();
            }
        }

        private void UpdateDetail(HorrorInventorySlotView slot)
        {
            _selectedSlot = slot;
            _slotDetailView.SetSlotDetail(slot.SlotInfo);
        }

        public bool IsSubmenuOpen()
            => _contextMenu != null && _contextMenu.IsOpen;

        // サブメニュー展開：非空スロットの決定で種別に応じたエントリを開く。空スロットは無視。
        private void OpenSubmenu(HorrorInventorySlotView slot)
        {
            if (IsSubmenuOpen()) return;
            if (slot == null || slot.SlotInfo == null) return;

            var entries = slot.SlotInfo.ToContextActions();
            if (entries.Length == 0) return;

            // 同一スロットの再クリックでは OnSelected が再発火しないため、決定時にも自身の引数で更新する
            _selectedSlot = slot;
            SetSlotsInteractable(false);
            _contextMenu.Open(slot.RectTransform, entries);
        }

        /// <summary>サブメニューを閉じる（キャンセル・アクション確定時に Dialog から呼ぶ）。</summary>
        public void CloseSubmenu()
        {
            if (_contextMenu != null) _contextMenu.Close();
        }

        // 閉じられたらグリッド操作を戻し、フォーカスを対象スロットへ復帰させる
        // （Close() は未オープン時に OnClosed を発火しないため、一回性のガードは不要）。
        private void OnSubmenuClosed()
        {
            SetSlotsInteractable(true);
            if (_selectedSlot != null)
                _inputService.SetSelectedGameObject(_selectedSlot.Selectable.gameObject);
        }

        private void SetSlotsInteractable(bool value)
        {
            if (_slotsCanvasGroup == null) return;
            _slotsCanvasGroup.interactable = value;
            _slotsCanvasGroup.blocksRaycasts = value;
        }

        #endregion

        #region KeyItems

        private void BindKeyItems()
        {
            foreach (Transform keyItem in _keyItemContentRoot)
            {
                keyItem.gameObject.SafeDestroy();
            }

            var keyItems = _keyItemService.KeyItems;
            foreach (var item in keyItems)
            {
                var keyItem = Instantiate(_keyItemPrefab, _keyItemContentRoot);
                keyItem.Initialize();
                keyItem.SetItem(item.ObjectCategory, item.Id);
            }
        }

        #endregion

        #region Craft

        /// <summary>クラフトの長押しが進行中か（ダイアログを閉じる・タブを切り替える入力の抑止に使う）。</summary>
        public bool IsCrafting() => _craftView != null && _craftView.IsCrafting;

        private void BindCraft()
        {
            if (_craftView == null) return;

            _craftView.Initialize();

            // クラフトはインベントリの中身を変えるため、グリッド表示へ反映する
            _craftView.OnCrafted
                .Subscribe(_ => ApplySlots())
                .AddTo(Disposables);
        }

        #endregion
    }
}
