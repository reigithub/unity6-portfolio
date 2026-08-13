using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
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
        , IGameSceneArg<IObjectInfo>
    {
        protected override string AssetPathOrAddress => "HorrorEquipmentShortcutDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IHorrorUISoundService _uiSoundService = GameServiceManager.Resolve<IHorrorUISoundService>();
        private IObjectInfo _target;

        public static async UniTask<bool> RunAsync(IObjectInfo target)
        {
            var sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorEquipmentShortcutDialog, IObjectInfo, bool>(target);
        }

        public UniTask SetArg(IObjectInfo target)
        {
            _target = target;
            return UniTask.CompletedTask;
        }

        public override UniTask Startup()
        {
            // キャンセルで閉じる（親がインベントリ表示中は Menu をブロックしているため Cancel のみ）
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    _uiSoundService.PlayCancelSfx();
                    TrySetResult(false);
                })
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
