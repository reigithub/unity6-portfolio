using Game.Shared.Enums;
using Game.Shared.Interfaces;

namespace Game.Horror.Inventory
{
    public struct HorrorInventoryContextActionInfo
    {
        public ContextActionType ContextActionType { get; set; }

        public IHorrorInventorySlotInfo SlotInfo { get; set; }
    }
}
