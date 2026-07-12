using System.Collections.Generic;
using System.Linq;
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

        /// <summary>いずれかのスロット行がクリックされたときに、選択されたスロット番号（1〜）を通知する。</summary>
        public Observable<int> OnSlotSelected
            => Observable.Merge(_slotViews.Select((view, i) => view.OnClick.Select(_ => i + 1)));

        /// <summary>
        /// 全スロットのメタ情報を表示テキストへ解決して各行に反映する。
        /// 名称はマスターからローカライズ解決し、空きスロットは名称・日時とも空文字。
        /// 範囲外・未配線のスロット番号、マスター不在の SavepointId は LogError で顕在化する。
        /// </summary>
        public void SetSlotInfos(IReadOnlyList<HorrorSaveSlotInfo> slots)
        {
            var database = GameServiceManager.Resolve<IScriptableDatabaseService>().Database;
            var localization = GameServiceManager.Resolve<ILocalizationService>();

            foreach (var slot in slots)
            {
                if (slot.SlotNo < 1 || slot.SlotNo > _slotViews.Length || _slotViews[slot.SlotNo - 1] == null)
                {
                    Debug.LogError($"[{GetType().Name}] Invalid or unwired slot: {slot.SlotNo}");
                    continue;
                }

                var savepointName = string.Empty;
                var dateTimeText = string.Empty;

                if (slot.HasData)
                {
                    if (database.HorrorInteractionMasterTable.TryFindById(slot.SavepointId, out var master))
                        savepointName = localization.GetStringByInteractions(master.InteractionLocalizeKey);
                    else
                        Debug.LogError($"[{GetType().Name}] HorrorInteractionMaster not found: SavepointId={slot.SavepointId}");

                    dateTimeText = slot.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
                }

                _slotViews[slot.SlotNo - 1].SetInfo(slot.SlotNo.ToString(), savepointName, dateTimeText);
            }
        }
    }
}
