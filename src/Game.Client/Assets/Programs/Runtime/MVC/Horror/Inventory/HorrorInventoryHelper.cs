using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.Scriptable.Database;

namespace Game.Horror.Inventory
{
    public static class HorrorInventoryHelper
    {
        public static bool TryGetSlotInfo(ScriptableDatabase database, InventorySlotType type, int id, out IHorrorInventorySlotInfo info)
        {
            info = null;
            switch (type)
            {
                case InventorySlotType.Item:
                    if (database.HorrorItemMasterTable.TryFindById(id, out var itemMaster))
                    {
                        info = itemMaster;
                        return true;
                    }
                    break;
                case InventorySlotType.Weapon:
                    if (database.HorrorWeaponMasterTable.TryFindById(id, out var weaponMaster))
                    {
                        info = weaponMaster;
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}
