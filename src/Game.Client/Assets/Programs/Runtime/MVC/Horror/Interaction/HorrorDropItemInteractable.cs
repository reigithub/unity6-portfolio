using System;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// エネミードロップ品の拾得インタラクト。アイテム・数量は <see cref="Setup"/> で実行時注入され、
    /// マスター共有行（_interactionId）はプロンプト表示・入力方式の供給のみに使う。
    /// シーン静的配置の <see cref="HorrorItemInteractable"/> と異なりランタイム生成されるため、
    /// インタラクション永続化（IHorrorInteractionService）には一切書き込まない
    /// （共有 Id を記録すると同 Id の全ドロップ品が取得済み扱いになる）。
    /// </summary>
    public class HorrorDropItemInteractable : InteractableBase
    {
        private HorrorItemMaster _itemMaster;
        private int _count;
        private Action<HorrorDropItemInteractable> _onCollected;

        /// <summary>スポーナーがプール貸出時に呼ぶ。プール再利用個体の状態上書きを兼ねる。</summary>
        public void Setup(HorrorItemMaster itemMaster, int count, Action<HorrorDropItemInteractable> onCollected)
        {
            _itemMaster = itemMaster;
            _count = count;
            _onCollected = onCollected;
        }

        // ドロップ品に再拾得の概念はなく、取得状態を永続化もしない
        public override bool WasInteracted() => false;

        public override bool CanInteract() => _itemMaster != null;

        public override void Interact()
        {
            // キーアイテムはマスターデータ検証（HorrorEnemyDropItemValidator）で配布禁止のため通常アイテム経路のみ。
            // インベントリ満杯（TryAdd 失敗）時はその場に残置する
            var inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            if (!inventoryService.TryAdd(_itemMaster.ObjectCategory, _itemMaster.Id, _count, _itemMaster.MaxCount))
                return;

            // base.Interact()（= 共有 _interactionId の永続化）は呼ばず、スポーナーへプール返却を通知する
            _onCollected?.Invoke(this);
        }

        public override InteractionTargetInfo GetTargetInfo()
        {
            if (_itemMaster == null)
                return base.GetTargetInfo();

            return new InteractionTargetInfo
            {
                ObjectCategory = _itemMaster.ObjectCategory,
                Id = _itemMaster.Id,
                Name = _itemMaster.Name,
                Description = _itemMaster.Description,
                Count = _count,
                IconAssetName = _itemMaster.IconAssetName,
            };
        }
    }
}
