using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Inventory;
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

        #endregion

        private IInputSystemService _inputService;
        private IHorrorInventoryService _inventoryService;
        private IHorrorKeyItemService _keyItemService;

        private HorrorInventorySlotView _slotView;

        // 最後に詳細ペインへ反映したスロット。ApplySlots の再適用後に詳細表示を追随させるために保持する
        // （_slotView はサブメニュー閉時に null 化されるため流用できない）
        private HorrorInventorySlotView _lastSelectedSlot;

        public Observable<HorrorInventoryContextActionInfo> OnContextActionClicked
            => _contextMenu.OnClicked.Select(x => new HorrorInventoryContextActionInfo
            {
                ContextActionType = x,
                SlotView = _slotView
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

        #region InventorySlots

        // 初期化と購読は寿命中 1 回のみ。再実行すると Disposables に購読が重複登録されるため、
        // データの再反映は ApplySlots を使うこと。
        private void BindSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Initialize();
                _slots[i].OnSelected.Subscribe(UpdateDetail).AddTo(Disposables);
                _slots[i].OnSubmit.Subscribe(OpenSubmenu).AddTo(Disposables);
            }

            ApplySlots();
        }

        /// <summary>
        /// インベントリデータをスロット表示へ反映する（再入可能）。
        /// スロット破棄などでデータの並びが変わった後に呼び、グリッドと詳細ペインを最新化する。
        /// </summary>
        public void ApplySlots()
        {
            var slots = _inventoryService.Slots;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < slots.Count)
                {
                    var slot = slots[i];
                    _slots[i].SetSlot(i, slot.ObjectCategory, slot.Id, slot.Count);
                }
                else
                {
                    _slots[i].SetEmpty();
                }
            }

            if (_lastSelectedSlot != null)
                UpdateDetail(_lastSelectedSlot);
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
            _lastSelectedSlot = slot;
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

            _slotView = slot;
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
            if (_slotView != null)
            {
                _inputService.SetSelectedGameObject(_slotView.Selectable.gameObject);
                _slotView = null;
            }
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
    }
}
