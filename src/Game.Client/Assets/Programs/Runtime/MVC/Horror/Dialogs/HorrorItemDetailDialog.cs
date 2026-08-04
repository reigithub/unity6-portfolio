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
    /// アイテム詳細ダイアログ。インベントリのサブメニュー「調べる」からネストで開き、
    /// 対象アイテムの名称・説明・（武器なら）SPECS・3D プレビューを表示する。
    /// 時間停止は親（インベントリ）が保持するため触れない。
    /// </summary>
    public class HorrorItemDetailDialog : GameDialogScene<HorrorItemDetailDialog, HorrorItemDetailDialogComponent, bool>
        , IGameSceneArg<IObjectInfo>
    {
        protected override string AssetPathOrAddress => "HorrorItemDetailDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private IObjectInfo _target;

        public static async UniTask<bool> RunAsync(IObjectInfo target)
        {
            var sceneService = GameServiceManager.Resolve<IGameSceneService>();
            return await sceneService.TransitionDialogAsync<HorrorItemDetailDialog, IObjectInfo, bool>(target);
        }

        public UniTask SetArg(IObjectInfo target)
        {
            _target = target;
            return UniTask.CompletedTask;
        }

        public override async UniTask Startup()
        {
            // キャンセルで閉じる（親がインベントリ表示中は Menu をブロックしているため Cancel のみ）
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // R でプレビューの回転・ズームをリセット
            _inputService.UI.Reset.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ => SceneComponent.ResetPreview())
                .AddTo(Disposables);

            await SceneComponent.InitializeAsync(_target);
            await base.Startup();
        }
    }
}
