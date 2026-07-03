using Game.Shared.Enums;
using Game.Shared.Interfaces;

namespace Game.Shared.Scriptable.Database.Tables
{
    public partial class HorrorWeaponMaster : IHorrorInventorySlotInfo
    {
        public InventorySlotType SlotType => InventorySlotType.Weapon;
    }
}
