using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using R3;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// セーブスロット選択ダイアログ。セーブポイントアクセス時に開き、保存先スロットを選ばせる。
    /// </summary>
    public class HorrorSaveDataDialog : GameDialogScene<HorrorSaveDataDialog, HorrorSaveDataDialogComponent, int>
        , IGameSceneArg<IReadOnlyList<HorrorSaveSlotInfo>>
    {
        protected override string AssetPathOrAddress => "HorrorSaveDataDialog";

        private InputSystemService _inputService;
        private IReadOnlyList<HorrorSaveSlotInfo> _slots;

        /// <summary>
        /// ダイアログを開き、選択されたスロット番号を返す。
        /// </summary>
        /// <param name="slots">全スロットのメタ情報。</param>
        /// <returns>選択スロット番号（1〜スロット数上限）。0 はキャンセル。</returns>
        public static async UniTask<int> RunAsync(IReadOnlyList<HorrorSaveSlotInfo> slots)
        {
            int result;
            var inputService = GameServiceManager.Get<InputSystemService>();
            using (inputService.BlockPlayer())
            using (inputService.BlockInputActions(inputService.UI.Menu, inputService.UI.Inventory))
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorSaveDataDialog, IReadOnlyList<HorrorSaveSlotInfo>, int>(slots);
            }
            return result;
        }

        public UniTask SetArg(IReadOnlyList<HorrorSaveSlotInfo> slots)
        {
            _slots = slots;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Get<InputSystemService>();
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            SceneComponent.OnSlotSelected
                .Subscribe(slotNumber =>
                {
                    SceneComponent.SetInteractable(false);
                    TrySetResult(slotNumber);
                })
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
