using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorInventoryDialog : GameDialogScene<HorrorInventoryDialog, HorrorInventoryDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "HorrorInventoryDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();

        public static async UniTask<bool> RunAsync()
        {
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            bool result;
            using (inputService.BlockPlayer())
            using (inputService.BlockInputAction(inputService.UI.Menu))
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorInventoryDialog, bool>();
            }
            return result;
        }

        public override UniTask PreInitialize()
        {
            ApplicationEvents.PauseTime();
            return base.PreInitialize();
        }

        public override UniTask Startup()
        {
            // キャンセル：サブメニュー展開中は一段だけ閉じ、それ以外はダイアログを閉じる
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    if (SceneComponent.IsSubmenuOpen())
                        SceneComponent.CloseSubmenu();
                    else
                        TrySetResult(default);
                })
                .AddTo(Disposables);

            // インベントリトグルでダイアログを閉じる（サブメニュー展開中は無効）
            _inputService.UI.Inventory.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen())
                .Subscribe(_ => TrySetResult(default))
                .AddTo(Disposables);

            // L1 (Previous) / R1 (Next) でタブ循環（サブメニュー展開中は無効）
            _inputService.UI.Previous.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen())
                .Subscribe(_ => SceneComponent.PreviousTab())
                .AddTo(Disposables);

            _inputService.UI.Next.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen())
                .Subscribe(_ => SceneComponent.NextTab())
                .AddTo(Disposables);

            // アクション選択：Shortcut はショートカット登録ダイアログをネストで開く。他は従来通り。
            SceneComponent.OnContextActionClicked
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (info, _) =>
                {
                    var slotInfo = info.SlotInfo;
                    SceneComponent.CloseSubmenu();

                    if (info.ContextActionType == InventoryContextActionType.Shortcut)
                        await HorrorEquipmentShortcutDialog.RunAsync(slotInfo);
                    else
                        Debug.Log($"[HorrorInventory] Action selected: {info.ContextActionType}");
                })
                .AddTo(Disposables);

            SceneComponent.Initialize();

            return base.Startup();
        }

        public override async UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            await base.Terminate();
        }
    }
}
