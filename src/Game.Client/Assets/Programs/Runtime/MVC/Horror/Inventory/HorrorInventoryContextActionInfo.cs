using Game.Shared.Enums;

namespace Game.Horror.Inventory
{
    public struct HorrorInventoryContextActionInfo
    {
        public ContextActionType ContextActionType { get; init; }

        public HorrorInventorySlotView SlotView { get; init; }
    }
}
