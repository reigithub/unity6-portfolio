using System.Collections.Generic;
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

        private readonly IScriptableDatabaseService _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
        private readonly ILocalizationService _localizationService = GameServiceManager.Resolve<ILocalizationService>();

        /// <summary>いずれかのスロット行がクリックされたときに、選択されたスロット番号（1〜）を通知する。</summary>
        private readonly Subject<int> _onSlotClick = new();
        public Observable<int> OnSlotClick => _onSlotClick;

        private readonly Subject<int> _onSlotSelect = new();
        public Observable<int> OnSlotSelect => _onSlotSelect;

        public void SetSlotInfo(HorrorSaveSlotInfo slot)
        {
            var savepointName = string.Empty;
            var dateTimeText = string.Empty;

            if (slot.HasData)
            {
                if (_databaseService.Database.HorrorInteractionMasterTable.TryFindById(slot.SavepointId, out var master))
                    savepointName = _localizationService.GetStringByInteractions(master.InteractionLocalizeKey);
                else
                    Debug.LogError($"[{GetType().Name}] HorrorInteractionMaster not found: SavepointId={slot.SavepointId}");

                dateTimeText = slot.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }

            var slotView = _slotViews[slot.SlotNo - 1];
            slotView.SetInfo(slot.SlotNo, savepointName, dateTimeText);
            slotView.OnClick.Subscribe(_ => _onSlotClick.OnNext(slot.SlotNo)).AddTo(Disposables);
            slotView.OnSelect.Subscribe(_ => _onSlotSelect.OnNext(slot.SlotNo)).AddTo(Disposables);
        }

        /// <summary>
        /// 全スロットのメタ情報を表示テキストへ解決して各行に反映する。
        /// 名称はマスターからローカライズ解決し、空きスロットは名称・日時とも空文字。
        /// 範囲外・未配線のスロット番号、マスター不在の SavepointId は LogError で顕在化する。
        /// </summary>
        public void SetSlotInfos(IReadOnlyList<HorrorSaveSlotInfo> slots)
        {
            foreach (var slot in slots)
            {
                if (slot.SlotNo < 1 || slot.SlotNo > _slotViews.Length || _slotViews[slot.SlotNo - 1] == null)
                {
                    Debug.LogError($"[{GetType().Name}] Invalid or unwired slot: {slot.SlotNo}");
                    continue;
                }

                SetSlotInfo(slot);
            }
        }
    }
}
