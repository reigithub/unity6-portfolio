using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Services
{
    public class HorrorCheckpointSaveService : IHorrorCheckpointSaveService, IGameService
    {
        private readonly IHorrorInteractionSaveService _interaction;
        private readonly IHorrorInventorySaveService _inventory;
        private readonly IHorrorEquipmentSaveService _equipment;

        public HorrorCheckpointSaveService(
            IHorrorInteractionSaveService interaction,
            IHorrorInventorySaveService inventory,
            IHorrorEquipmentSaveService equipment)
        {
            _interaction = interaction;
            _inventory = inventory;
            _equipment = equipment;
        }

        public async UniTask SaveIfDirtyAsync()
        {
            await _interaction.SaveIfDirtyAsync();
            await _inventory.SaveIfDirtyAsync();
            await _equipment.SaveIfDirtyAsync();
        }
    }
}
