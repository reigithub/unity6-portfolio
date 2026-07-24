using Game.Shared.Enums;
using Game.Shared.Interfaces;

namespace Game.Shared.Scriptable.Database.Tables
{
    public partial class HorrorWeaponMaster : IObjectInfo
    {
        public ObjectCategory ObjectCategory => ObjectCategory.Weapon;
        public int ObjectId => Id;

        public bool HasEffect => false;
    }
}
