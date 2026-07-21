using Game.Horror.SaveData;
using Game.Shared.SaveData;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorOptionSaveRepository : ISaveRepository<HorrorOptionSaveData>, IGameService
    {
    }
}
