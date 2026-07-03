using Game.Shared.Enums;
using Game.Shared.Interfaces;

namespace Game.Horror.Inventory
{
    public struct HorrorInventoryContextActionInfo
    {
        public InventoryContextActionType ContextActionType { get; set; }

        public IHorrorInventorySlotInfo SlotInfo { get; set; }
    }
}
