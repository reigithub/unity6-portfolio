using Game.Core.Services;
using Game.Horror.SaveData;
using Game.MVC.Core.Scenes;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorSaveDataDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private HorrorSaveSlotView[] _slotViews;

        private IScriptableDatabaseService _databaseService;
        private ILocalizationService _localizationService;

        /// <summary>いずれかのスロット行がクリックされたときに、選択されたスロット番号（0〜）を通知する。</summary>
        private readonly Subject<int> _onSlotClick = new();
        public Observable<int> OnSlotClick => _onSlotClick;

        private readonly Subject<int> _onSlotSelect = new();
        public Observable<int> OnSlotSelect => _onSlotSelect;

        /// <summary>
        /// 全スロットのメタ情報を表示テキストへ解決して各行に反映する。
        /// </summary>
        public void SetSlotInfos(HorrorSaveSlotInfo[] slots)
        {
            _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();

            foreach (var slot in slots)
            {
                SetSlotInfo(slot);
            }
        }

        /// <summary>
        /// スロット行にメタ情報を反映する。名称はマスターからローカライズ解決し、空きスロットは名称・日時とも空文字。
        /// </summary>
        public void SetSlotInfo(HorrorSaveSlotInfo slot)
        {
            if (slot.SlotNo < 0 || slot.SlotNo >= _slotViews.Length || _slotViews[slot.SlotNo] == null)
            {
                Debug.LogError($"[{GetType().Name}] Invalid or unwired slot: {slot.SlotNo}");
                return;
            }

            var savepointName = string.Empty;
            var dateTimeText = string.Empty;

            if (slot.HasData)
            {
                if (_databaseService.Database.HorrorInteractionMasterTable.TryFindById(slot.SavepointId, out var master))
                    savepointName = _localizationService.GetStringByInteractions(master.Name);
                else
                    Debug.LogError($"[{GetType().Name}] HorrorInteractionMaster not found: SavepointId={slot.SavepointId}");

                dateTimeText = slot.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }

            var slotView = _slotViews[slot.SlotNo];
            slotView.SetInfo(slot.SlotNo + 1, savepointName, dateTimeText);
            slotView.OnClick.Subscribe(_ => _onSlotClick.OnNext(slot.SlotNo)).AddTo(Disposables);
            slotView.OnSelect.Subscribe(_ => _onSlotSelect.OnNext(slot.SlotNo)).AddTo(Disposables);
        }
    }
}
