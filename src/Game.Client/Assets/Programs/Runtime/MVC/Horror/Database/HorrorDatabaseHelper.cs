using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.Scriptable.Database;

namespace Game.Horror.Database
{
    public static class HorrorDatabaseHelper
    {
        public static bool TryGetInfo(ScriptableDatabase database, ObjectCategory category, int id, out IObjectInfo info)
        {
            info = null;
            switch (category)
            {
                case ObjectCategory.Item:
                    if (database.HorrorItemMasterTable.TryFindById(id, out var itemMaster))
                    {
                        info = itemMaster;
                        return true;
                    }
                    break;
                case ObjectCategory.Weapon:
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
