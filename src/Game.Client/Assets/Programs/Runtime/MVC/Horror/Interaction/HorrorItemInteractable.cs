using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールドのアイテムを拾うインタラクト。マスターデータの GrantItemId をインベントリへ加え、自身を非表示にする。
    /// 効果（付与アイテム・数量）はマスターデータから引き、コードは「拾って付与する」振る舞いのみを担う。
    /// </summary>
    public class HorrorItemInteractable : InteractableBase
    {
        protected override void Start()
        {
            base.Start();
            gameObject.SetActive(!WasInteracted());
        }

        public override bool CanInteract() => !WasInteracted();

        public override void Interact()
        {
            if (!TryPickUpItem()) return;

            gameObject.SetActive(false);

            base.Interact();
        }

        public override InteractionTargetInfo GetTargetInfo()
        {
            if (Master == null || !Database.HorrorItemMasterTable.TryFindById(Master.AcquiredId, out var master))
                return base.GetTargetInfo();

            return new InteractionTargetInfo
            {
                ObjectCategory = master.ObjectCategory,
                Id = master.Id,
                Name = master.Name,
                Description = master.Description,
                Count = Master.AcquiredCount,
                IconAssetName = master.IconAssetName
            };
        }

        private bool TryPickUpItem()
        {
            if (Master == null || !Database.HorrorItemMasterTable.TryFindById(Master.AcquiredId, out var master))
                return false;

            if (master.KeyItem)
            {
                var keyItemService = GameServiceManager.Resolve<IHorrorKeyItemService>();
                return keyItemService.TryAdd(master.ObjectCategory, master.Id, Master.AcquiredCount);
            }

            var inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            return inventoryService.TryAdd(master.ObjectCategory, master.Id, Master.AcquiredCount, master.MaxCount);
        }
    }
}
