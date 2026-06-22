using Game.Core.Services;
using Game.Horror.Inventory;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールドのアイテムを拾うインタラクト。マスターデータの GrantItemId をインベントリへ加え、自身を非表示にする。
    /// 効果（付与アイテム・数量）はマスターデータから引き、コードは「拾って付与する」振る舞いのみを担う。
    /// </summary>
    public class PickItemInteractable : InteractableBase
    {
        public override void Interact()
        {
            if (Master == null)
                return;

            var database = GameServiceManager.Get<ScriptableDatabaseService>().Database;
            if (database.HorrorItemMasterTable.TryFindById(Master.GrantItemId, out var itemMaster))
                GameServiceManager.Get<HorrorInventoryService>().Add(itemMaster, Master.GrantQuantity);

            gameObject.SetActive(false);
        }
    }
}
