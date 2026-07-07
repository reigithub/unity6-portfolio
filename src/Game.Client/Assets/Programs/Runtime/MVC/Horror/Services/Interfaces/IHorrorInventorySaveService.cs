using Cysharp.Threading.Tasks;
using Game.Shared.Enums;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorInventorySaveService
    {
        UniTask SaveIfDirtyAsync();

        /// <summary>指定 (SlotType, Id) を所持しているか判定する。</summary>
        bool HasItem(InventorySlotType type, int id);
    }
}
