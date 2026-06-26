namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールドのアイテムを拾うインタラクト。マスターデータの GrantItemId をインベントリへ加え、自身を非表示にする。
    /// 効果（付与アイテム・数量）はマスターデータから引き、コードは「拾って付与する」振る舞いのみを担う。
    /// </summary>
    public class PickItemInteractable : InteractableBase
    {
        protected override void Start()
        {
            base.Start();

            gameObject.SetActive(!WasInteracted());
        }

        public override bool CanInteract() => !WasInteracted();

        public override void Interact()
        {
            if (Master == null)
                return;

            if (Database.HorrorItemMasterTable.TryFindById(Master.GrantItemId, out var itemMaster))
                InventorySaveService.Add(itemMaster, Master.GrantQuantity);

            gameObject.SetActive(false);

            base.Interact();
        }
    }
}
