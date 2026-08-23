using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Horror.Inventory;
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
        public int UseSlotNo { get; init; }

        public bool HasUseRequest => UseCategory != ObjectCategory.None;
    }

    public class HorrorInventoryDialog : GameDialogScene<HorrorInventoryDialog, HorrorInventoryDialogComponent, HorrorInventoryResult>
    {
        protected override string AssetPathOrAddress => "HorrorInventoryDialog";

        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IHorrorInventoryService _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
        private readonly IHorrorPlayerService _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();
        private readonly IHorrorUISoundService _uiSoundService = GameServiceManager.Resolve<IHorrorUISoundService>();
        private readonly IHorrorCraftService _craftService = GameServiceManager.Resolve<IHorrorCraftService>();

        private HorrorInventoryResult _result;
        private bool _isCraftHolding; // 長押し進行中（Cancel・タブ切替・トグルの抑止用）

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
            // キャンセル：サブメニュー展開中は一段だけ閉じ、それ以外はダイアログを閉じる（クラフトの長押し中は無効）
            _inputService.UI.Cancel.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    if (_isCraftHolding) return;

                    _uiSoundService.PlayCancelSfx();

                    if (SceneComponent.IsSubmenuOpen())
                        SceneComponent.CloseSubmenu();
                    else
                        TrySetResult(_result);
                })
                .AddTo(Disposables);

            // インベントリトグルでダイアログを閉じる（サブメニュー展開中・クラフトの長押し中は無効）
            _inputService.Player.Inventory.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen() && !_isCraftHolding)
                .Subscribe(_ => TrySetResult(_result))
                .AddTo(Disposables);

            // L1 (Previous) / R1 (Next) でタブ循環（サブメニュー展開中・クラフトの長押し中は無効）
            _inputService.UI.Previous.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen() && !_isCraftHolding)
                .Subscribe(_ => SceneComponent.PreviousTab())
                .AddTo(Disposables);

            _inputService.UI.Next.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing() && !SceneComponent.IsSubmenuOpen() && !_isCraftHolding)
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
                                UseSlotNo = ctx.SlotView.SlotIndex,
                            };
                            TrySetResult(_result);
                            break;
                        case ContextActionType.Inspect:
                            await HorrorItemDetailDialog.RunAsync(info);
                            break;
                        case ContextActionType.Discard:
                            var result = await HorrorConfirmDialog.RunAsync("Confirm_Discard_Item");
                            if (!result) return;

                            _inventoryService.DiscardSlot(ctx.SlotView.SlotIndex);
                            break;
                        case ContextActionType.Equip:
                            _result = new HorrorInventoryResult { EquipCategory = info.ObjectCategory, EquipId = info.ObjectId };
                            break;
                        case ContextActionType.Shortcut:
                            await HorrorEquipmentShortcutDialog.RunAsync(info);
                            break;
                    }
                })
                .AddTo(Disposables);

            // クラフトの長押し：決定またはレシピ行のポインタ押下で開始し、閾値到達で実行（ゲージの描画は View 側）
            var craftView = SceneComponent.CraftView;
            if (craftView != null)
            {
                Observable.Merge(_inputService.UI.Submit.OnPerformedAsObservable().AsUnitObservable(),
                        craftView.OnRecipePointerPressed)
                    .Where(_ => State.IsProcessing() && !_isCraftHolding && craftView.IsVisible)
                    .SubscribeAwait(async (_, ct) => await RunCraftHoldAsync(craftView, ct))
                    .AddTo(Disposables);
            }

            SceneComponent.Initialize();

            return base.Startup();
        }

        public override async UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            await base.Terminate();
        }

        // 長押し1回分のフロー。エッジ（決定押下・行ポインタ押下）で開始し、中断または実行で終わる。
        // 押しっぱなしでのタブ進入・実行直後の押し続けは新しいエッジを生まないため、再実行は構造的に起きない。
        private async UniTask RunCraftHoldAsync(HorrorCraftView view, CancellationToken ct = default)
        {
            _isCraftHolding = true;
            var elapsed = 0f;
            try
            {
                // 押下と同フレームの選択更新（ポインタ押下による行選択）を待ってから対象を確定する
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                var craftId = view.SelectedCraftId;
                if (craftId == null) return;

                while (true)
                {
                    if (!State.IsProcessing() || !view.IsVisible || view.SelectedCraftId != craftId) return;

                    bool held = _inputService.UI.Submit.IsPressed() || view.IsSelectedPointerHeld;

                    // 中断条件：押下解除・素材不足（他タブでの破棄などで実行不可へ変わった場合を含む）
                    if (!held || !_craftService.CanCraft(craftId.Value)) return;

                    elapsed += Time.unscaledDeltaTime;
                    view.SetHoldProgress(elapsed / HorrorCraftConstants.CraftHoldSeconds);

                    if (elapsed >= HorrorCraftConstants.CraftHoldSeconds)
                    {
                        view.SetHoldProgress(0f); // 実行前にゲージを消す（旧 Execute と同順）
                        _craftService.TryCraft(craftId.Value);
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                _isCraftHolding = false;
                view.SetHoldProgress(0f); // 中断時の後始末（冪等。View 破棄後も SetHoldProgress 内の null ガードで安全）
            }
        }
    }
}
