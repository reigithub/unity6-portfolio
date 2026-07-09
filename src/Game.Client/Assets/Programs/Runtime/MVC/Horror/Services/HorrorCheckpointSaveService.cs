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
        private readonly IHorrorRespawnSaveService _respawn;

        public HorrorCheckpointSaveService(
            IHorrorInteractionSaveService interaction,
            IHorrorInventorySaveService inventory,
            IHorrorEquipmentSaveService equipment,
            IHorrorRespawnSaveService respawn)
        {
            _interaction = interaction;
            _inventory = inventory;
            _equipment = equipment;
            _respawn = respawn;
        }

        public async UniTask SaveIfDirtyAsync()
        {
            await _interaction.SaveIfDirtyAsync();
            await _inventory.SaveIfDirtyAsync();
            await _equipment.SaveIfDirtyAsync();
            await _respawn.SaveIfDirtyAsync();
        }
    }
}
