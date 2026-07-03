using Game.Shared.Enums;

namespace Game.Shared.Interfaces
{
    public interface IHorrorInventorySlotInfo
    {
        InventorySlotType SlotType { get; }
        int Id { get; }
        string Name { get; }
        string Description { get; }
        string IconAssetName { get; }
        int MaxCount { get; }
    }
}
