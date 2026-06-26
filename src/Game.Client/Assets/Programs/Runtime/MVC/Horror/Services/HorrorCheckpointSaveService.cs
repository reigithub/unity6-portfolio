using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Services
{
    public class HorrorCheckpointSaveService : IHorrorCheckpointSaveService, IGameService
    {
        private readonly IHorrorInteractionSaveService _interaction;
        private readonly IHorrorInventorySaveService _inventory;

        public HorrorCheckpointSaveService(IHorrorInteractionSaveService interaction, IHorrorInventorySaveService inventory)
        {
            _interaction = interaction;
            _inventory = inventory;
        }

        public async UniTask SaveIfDirtyAsync()
        {
            await _interaction.SaveIfDirtyAsync();
            await _inventory.SaveIfDirtyAsync();
        }
    }
}
