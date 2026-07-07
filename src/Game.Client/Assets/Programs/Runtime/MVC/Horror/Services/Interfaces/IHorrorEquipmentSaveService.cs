using Cysharp.Threading.Tasks;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorEquipmentSaveService
    {
        UniTask SaveIfDirtyAsync();
    }
}
