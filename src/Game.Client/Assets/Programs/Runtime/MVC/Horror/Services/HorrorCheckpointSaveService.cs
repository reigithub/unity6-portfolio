using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Services
{
    public class HorrorCheckpointSaveService : IHorrorCheckpointSaveService, IGameService
    {
        private readonly IHorrorInteractionSaveService _interaction;
        private readonly IHorrorInventorySaveService _inventory;
        private readonly IHorrorEquipmentShortcutSaveService _equipmentShortcut;

        public HorrorCheckpointSaveService(
            IHorrorInteractionSaveService interaction,
            IHorrorInventorySaveService inventory,
            IHorrorEquipmentShortcutSaveService equipmentShortcut)
        {
            _interaction = interaction;
            _inventory = inventory;
            _equipmentShortcut = equipmentShortcut;
        }

        public async UniTask SaveIfDirtyAsync()
        {
            await _interaction.SaveIfDirtyAsync();
            await _inventory.SaveIfDirtyAsync();
            await _equipmentShortcut.SaveIfDirtyAsync();
        }
    }
}
