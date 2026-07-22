using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールドの武器を拾うインタラクト。マスターデータの GrantItemId が指す武器を
    /// インベントリへ加え、自身を非表示にする。付与対象を武器マスターから引く点だけが
    /// <see cref="HorrorItemInteractable"/> と異なり、拾得の振る舞いは同型。
    /// </summary>
    public class HorrorWeaponInteractable : InteractableBase
    {
        protected override void Start()
        {
            base.Start();

            gameObject.SetActive(!WasInteracted());
        }

        public override bool CanInteract() => !WasInteracted();

        public override void Interact()
        {
            if (!TryPickUpWeapon()) return;

            gameObject.SetActive(false);

            base.Interact();
        }

        public override InteractionTargetInfo GetTargetInfo()
        {
            if (Master == null || !Database.HorrorWeaponMasterTable.TryFindById(Master.AcquiredId, out var master))
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

        private bool TryPickUpWeapon()
        {
            if (Master == null || !Database.HorrorWeaponMasterTable.TryFindById(Master.AcquiredId, out var master))
                return false;

            var inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            return inventoryService.TryAdd(master.ObjectCategory, master.Id, Master.AcquiredCount, master.MaxCount);
        }
    }
}
