using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    public record HorrorSaveDataDialogArgs
    {
        public HorrorSaveSlotInfo[] Slots;
        public bool SaveMode = true;
    }

    /// <summary>
    /// セーブスロット選択ダイアログ。セーブポイントアクセス時に開き、保存先スロットを選ばせる。
    /// </summary>
    public class HorrorSaveDataDialog : GameDialogScene<HorrorSaveDataDialog, HorrorSaveDataDialogComponent, int>
        , IGameSceneArg<HorrorSaveDataDialogArgs>
    {
        protected override string AssetPathOrAddress => "HorrorSaveDataDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IHorrorSaveRepository _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
        private HorrorSaveSlotInfo[] _slots;
        private int _slotNo = -1;
        private bool _saveMode = true;

        /// <summary>
        /// ダイアログを開き、選択されたスロット番号を返す。
        /// </summary>
        /// <param name="slots">全スロットのメタ情報。</param>
        /// <param name="saveMode">スロット選択時にセーブする</param>
        /// <returns>選択スロット番号（0〜スロット数上限 - 1）。負値はキャンセル。</returns>
        public static async UniTask<int> RunAsync(HorrorSaveSlotInfo[] slots, bool saveMode = true)
        {
            int result;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            using (inputService.BlockPlayer())
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                var args = new HorrorSaveDataDialogArgs { Slots = slots, SaveMode = saveMode };
                result = await sceneService.TransitionDialogAsync<HorrorSaveDataDialog, HorrorSaveDataDialogArgs, int>(args);
            }
            return result;
        }

        public UniTask SetArg(HorrorSaveDataDialogArgs args)
        {
            _slots = args.Slots;
            _saveMode = args.SaveMode;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(-1))
                .AddTo(Disposables);

            _inputService.UI.Remove.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) =>
                {
                    var result = await HorrorConfirmDialog.RunAsync("Confirm_Delete");
                    if (!result) return;

                    if (_slotNo < 0) return;
                    if (!_slots[_slotNo].HasData) return;
                    await _saveRepository.DeleteBySlotAsync(_slotNo);
                    _slots[_slotNo] = await _saveRepository.LoadSlotInfoAsync(_slotNo);
                    SceneComponent.SetSlotInfo(_slots[_slotNo]);
                })
                .AddTo(Disposables);

            SceneComponent.OnSlotClick
                .Subscribe(slotNo =>
                {
                    if (!_saveMode && !_slots[_slotNo].HasData) return;
                    SceneComponent.SetInteractable(false);
                    TrySetResult(slotNo);
                })
                .AddTo(Disposables);

            SceneComponent.OnSlotSelect
                .Subscribe(slotNo => _slotNo = slotNo)
                .AddTo(Disposables);

            SceneComponent.SetSlotInfos(_slots);

            return base.Startup();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            return base.Terminate();
        }
    }
}
