using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Extensions;
using Game.Shared.Interfaces;
using R3;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// 装備ショートカット登録ダイアログ。インベントリのサブメニュー「ショートカット」からネストで開き、
    /// 対象アイテムを D-Pad 4スロットへ登録／解除する。時間停止は親（インベントリ）が保持するため触れない。
    /// </summary>
    public class HorrorEquipmentShortcutDialog : GameDialogScene<HorrorEquipmentShortcutDialog, HorrorEquipmentShortcutDialogComponent, bool>
        , IGameSceneArg<IHorrorInventorySlotInfo>
    {
        protected override string AssetPathOrAddress => "HorrorEquipmentShortcutDialog";

        private IInputSystemService _inputService;
        private IHorrorInventorySlotInfo _target;

        public static async UniTask<bool> RunAsync(IHorrorInventorySlotInfo target)
        {
            var sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorEquipmentShortcutDialog, IHorrorInventorySlotInfo, bool>(target);
        }

        public UniTask SetArg(IHorrorInventorySlotInfo target)
        {
            _target = target;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            // キャンセルで閉じる（親がインベントリ表示中は Menu をブロックしているため Cancel のみ）
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // Del で現在スロットの登録を外す
            _inputService.UI.Remove.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.RemoveCurrent())
                .AddTo(Disposables);

            SceneComponent.Initialize(_target);

            return base.Startup();
        }
    }
}
