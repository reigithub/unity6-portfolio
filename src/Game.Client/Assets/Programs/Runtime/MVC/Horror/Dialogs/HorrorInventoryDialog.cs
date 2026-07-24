using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using R3;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public readonly struct HorrorInventoryResult
    {
        public ObjectCategory EquipCategory { get; init; }
        public int EquipId { get; init; }
        public bool HasEquipRequest => EquipCategory != ObjectCategory.None;

        public ObjectCategory UseCategory { get; init; }
        public int UseId { get; init; }
        public bool HasUseRequest => UseCategory != ObjectCategory.None;
    }

    public class HorrorInventoryDialog : GameDialogScene<HorrorInventoryDialog, HorrorInventoryDialogComponent, HorrorInventoryResult>
    {
        protected override string AssetPathOrAddress => "HorrorInventoryDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IHorrorInventoryService _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
        private readonly IHorrorPlayerService _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();

        private HorrorInventoryResult _result;

        public static async UniTask<HorrorInventoryResult> RunAsync()
        {
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            HorrorInventoryResult result;
            using (inputService.BlockPlayer(inputService.Player.Inventory))
            {
                var sceneService = GameServiceManager.Resolve<IGameSceneService>();
                result = await sceneService.TransitionDialogAsync<HorrorInventoryDialog, HorrorInventoryResult>();
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
                        TrySetResult(_result);
                })
                .AddTo(Disposables);

            // インベントリトグルでダイアログを閉じる（サブメニュー展開中は無効）
            _inputService.Player.Inventory.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen())
                .Subscribe(_ => TrySetResult(_result))
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
                .SubscribeAwait(async (ctx, _) =>
                {
                    var info = ctx.SlotView.SlotInfo;
                    SceneComponent.CloseSubmenu();

                    Debug.Log($"[HorrorInventory] Action selected: {ctx.ContextActionType}");
                    switch (ctx.ContextActionType)
                    {
                        case ContextActionType.Use:
                            // HP 満タン時は使用不可（無反応でダイアログに留まる）
                            if (_playerService.IsHealthFull)
                                break;

                            _result = new HorrorInventoryResult
                            {
                                EquipCategory = _result.EquipCategory,
                                EquipId = _result.EquipId,
                                UseCategory = info.ObjectCategory,
                                UseId = info.ObjectId,
                            };
                            TrySetResult(_result);
                            break;
                        case ContextActionType.Inspect:
                            // アイテム詳細ダイアログを実装して開く
                            // await HorrorItemDetailDialog.RunAsync(info);
                            break;
                        case ContextActionType.Discard:
                            _inventoryService.DiscardAll(info.ObjectCategory, info.ObjectId);
                            ctx.SlotView.SetEmpty();
                            break;
                        case ContextActionType.Equip:
                            _result = new HorrorInventoryResult { EquipCategory = info.ObjectCategory, EquipId = info.ObjectId };
                            break;
                        case ContextActionType.Shortcut:
                            await HorrorEquipmentShortcutDialog.RunAsync(info);
                            SceneComponent.RefreshSlots();
                            break;
                    }
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
