using System.Linq;
using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Scenes;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Horror.Dialogs
{
    public class HorrorSaveDataDialogComponent : GameSceneComponent
    {
        [SerializeField] private HorrorSaveSlotView[] _slotViews;
        [SerializeField] private InputActionGuildView _inputActionGuildView;

        private IScriptableDatabaseService _databaseService;
        private ILocalizationService _localizationService;
        private IHorrorSaveRepository _saveRepository;

        public Observable<int> OnSlotClick
            => _slotViews.Select((slot, index) => slot.OnClick.Select(_ => index)).Merge();

        public Observable<int> OnSlotSelect
            => _slotViews.Select((slot, index) => slot.OnSelect.Select(_ => index)).Merge();

        public void Initialize(HorrorSaveSlotInfo[] slots)
        {
            _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();

            _saveRepository.OnDataChanged
                .SubscribeAwait(async (data, _) =>
                {
                    var info = await _saveRepository.LoadSlotInfoAsync(data.SlotNo);
                    ApplySlotInfo(info);
                })
                .AddTo(Disposables);

            foreach (var slot in slots)
                ApplySlotInfo(slot);

            _inputActionGuildView.Initialize();
        }

        private void ApplySlotInfo(HorrorSaveSlotInfo slot)
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

            _slotViews[slot.SlotNo].SetInfo(slot.SlotNo + 1, savepointName, dateTimeText);
        }

        public void SetInputActionGuide(params InputAction[] actions)
            => _inputActionGuildView.SetInputActions(actions);
    }
}
